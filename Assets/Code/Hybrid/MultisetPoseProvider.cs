using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.ARFoundation;

namespace ARNav.Hybrid
{
    /// <summary>
    /// Pose source duy nhất cho indoor: lấy pose 6DoF của user từ MultiSet VPS,
    /// đồng thời convert về campus world qua <see cref="IndoorMapCalibration"/>.
    ///
    /// Có 2 đường lấy pose, dùng song song:
    ///   1. <b>Reflection (best-effort)</b>: nghe event <c>LocalizationSuccess</c> trên
    ///      <c>MapLocalizationManager</c> và quét response object để rút <c>mapId</c>,
    ///      <c>position</c>, <c>rotation</c>, <c>confidence</c>. Nếu SDK chỉ expose
    ///      UnityEvent no-arg thì cũng cố scan các field "last/current LocalizationResult".
    ///   2. <b>Fallback chắc chắn</b>: đọc pose camera trong MapSpace:
    ///        localCamPos = mapSpace.InverseTransformPoint(arCamera.position)
    ///        localCamRot = Quaternion.Inverse(mapSpace.rotation) * arCamera.rotation
    ///      rồi convert qua <see cref="IndoorMapCalibration.MapLocalToCampusWorld"/>.
    ///
    /// Component KHÔNG di chuyển XR Origin, KHÔNG mutate scene. Chỉ đọc và phơi pose qua property/event.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public class MultisetPoseProvider : MonoBehaviour
    {
        public struct PoseReading
        {
            public Vector3 MapLocalPosition;
            public Quaternion MapLocalRotation;
            public Vector3 CampusPosition;
            public Quaternion CampusRotation;
            public string MapId;
            public float Confidence;
            public bool ConfidenceAvailable;
            public bool MapIdAvailable;
            public double Timestamp;
            public PoseSource Source;
        }

        public enum PoseSource
        {
            None,
            ReflectionEvent,
            MapSpaceFallback,
        }

        [Header("References (auto-resolve nếu null)")]
        [Tooltip("AR camera dùng cho tracking indoor. Để null sẽ thử Camera.main rồi XR Origin.")]
        [SerializeField] private Camera arCamera;

        [Tooltip("Transform 'Map Space' do MultiSet SDK quản. Để null sẽ tự tìm theo tên 'Map Space'.")]
        [SerializeField] private Transform mapSpace;

        [Tooltip("MapLocalizationManager (com.multiset.sdk). Để null sẽ tự tìm MonoBehaviour có type name 'MapLocalizationManager'.")]
        [SerializeField] private MonoBehaviour mapLocalizationManager;

        [Header("Calibration")]
        [Tooltip("Calibration cho building hiện hành. Để null sẽ resolve runtime qua IndoorMapCalibration.FindFor.")]
        [SerializeField] private IndoorMapCalibration calibration;

        [Tooltip("Building hiện hành dùng để resolve calibration runtime. Có thể set bởi HybridLocalizationManager.")]
        [SerializeField] private BuildingId currentBuilding = BuildingId.None;

        [Tooltip("Floor id hiện hành dùng để resolve calibration runtime.")]
        [SerializeField] private string currentFloorId = "";

        [Header("Behavior")]
        [Tooltip("Khoảng refresh pose fallback (giây). 0 = mỗi LateUpdate.")]
        [SerializeField] private float fallbackUpdateIntervalSeconds = 0.1f;

        [Tooltip("Sau bao lâu (giây) không có event/fallback update thì coi pose là stale.")]
        [SerializeField] private float poseFreshnessWindowSeconds = 2f;

        [Tooltip("Bật fallback đọc MapSpace ngay cả khi reflection event hoạt động (an toàn hơn).")]
        [SerializeField] private bool alwaysRunFallback = true;

        [Tooltip("Fail-closed: pose MapSpace chỉ hợp lệ sau khi SDK đã phát LocalizationSuccess cho map hiện tại.")]
        [SerializeField] private bool requireVerifiedLocalizationForFallback = true;

        [Tooltip("Khi dùng pose fallback trên device, AR Session phải đang Tracking.")]
        [SerializeField] private bool requireARTrackingForFallback = true;

        [Header("Discovery")]
        [Tooltip("Mỗi N giây thử resolve lại các reference null (SDK instantiate trễ).")]
        [SerializeField] private float discoveryIntervalSeconds = 0.5f;

        [Header("Debug")]
        [SerializeField] private bool verboseLog = false;

        // ---------------------------------------------------------------------
        // Public read-only
        // ---------------------------------------------------------------------

        public PoseReading Last => _last;
        public bool HasVerifiedLocalization => _hasVerifiedLocalization;
        public float LastPoseAgeSeconds => _last.Source == PoseSource.None
            ? float.PositiveInfinity
            : Mathf.Max(0f, (float)(Time.timeAsDouble - _last.Timestamp));
        public bool HasFreshPose
        {
            get
            {
                if (_last.Source == PoseSource.None) return false;
                if ((Time.timeAsDouble - _last.Timestamp) > poseFreshnessWindowSeconds) return false;
                if (requireVerifiedLocalizationForFallback && !_hasVerifiedLocalization) return false;

                if (_last.Source == PoseSource.MapSpaceFallback &&
                    requireARTrackingForFallback &&
                    !Application.isEditor &&
                    ARSession.state != ARSessionState.SessionTracking)
                {
                    return false;
                }

                return true;
            }
        }

        public IndoorMapCalibration ActiveCalibration => calibration;
        public Transform MapSpace => mapSpace;
        public Camera ArCamera => arCamera;

        public event Action<PoseReading> OnPoseUpdated;

        public static MultisetPoseProvider FindActiveProvider()
        {
            foreach (var provider in FindObjectsByType<MultisetPoseProvider>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (provider != null && provider.isActiveAndEnabled)
                {
                    return provider;
                }
            }

            return null;
        }

        // ---------------------------------------------------------------------
        // Private state
        // ---------------------------------------------------------------------

        private PoseReading _last;
        private float _nextFallbackTime;
        private float _nextDiscoveryTime;
        private bool _hasVerifiedLocalization;
        private float _verifiedConfidence;
        private bool _verifiedConfidenceAvailable;
        private string _verifiedMapId;
        private bool _verifiedMapIdAvailable;
        private FieldInfo _localizedMapIdField;

        private MonoBehaviour _subscribedManager;
        private FieldInfo _subscribedEventField;
        private FieldInfo _subscribedFailureEventField;
        private UnityAction _noArgListener;
        private UnityAction _failureListener;
        // For typed event subscription
        private Delegate _typedListener;
        private MethodInfo _typedAddMethod;
        private MethodInfo _typedRemoveMethod;
        private FieldInfo _responseFieldOnManager;
        private Type _responseType;
        private FieldOrProp _respPosition, _respRotation, _respConfidence, _respMapId;

        // ---------------------------------------------------------------------
        // Public API
        // ---------------------------------------------------------------------

        public void SetCurrentBuilding(BuildingId building, string floorId)
        {
            bool changed = currentBuilding != building ||
                           !string.Equals(currentFloorId, floorId, StringComparison.Ordinal);
            if (changed && calibration != null)
                calibration.ClearRuntimeHandover();
            currentBuilding = building;
            currentFloorId = floorId;
            calibration = IndoorMapCalibration.FindFor(building, floorId);
            if (changed) ResetLocalizationSession();
        }

        /// <summary>
        /// Xoá VPS lease khi đổi map/rời indoor. Pose camera đơn thuần không được phép
        /// tái sử dụng như một localization success của map trước.
        /// </summary>
        public void ResetLocalizationSession()
        {
            _hasVerifiedLocalization = false;
            _verifiedConfidence = 0f;
            _verifiedConfidenceAvailable = false;
            _verifiedMapId = null;
            _verifiedMapIdAvailable = false;
            _last = default;
        }

        /// <summary>
        /// Bảo đảm pose indoor có cùng campus frame trước khi state machine công bố Indoor.
        /// Ưu tiên calibration khảo sát; nếu chưa có thì tạo bridge tại cửa từ pose VPS thật.
        /// </summary>
        public bool EnsureHandoverCalibration(EntranceAnchor entrance, Vector3 campusHandoverPosition)
        {
            if (!_hasVerifiedLocalization || entrance == null || arCamera == null || mapSpace == null)
                return false;

            var cal = ResolveCalibration();
            if (cal == null) return false;
            if (cal.HasAuthoredCalibration || cal.HasRuntimeHandoverCalibration) return true;

            Vector3 localPos = mapSpace.InverseTransformPoint(arCamera.transform.position);
            Quaternion localRot = Quaternion.Inverse(mapSpace.rotation) * arCamera.transform.rotation;
            Quaternion campusRot = Quaternion.Euler(0f, entrance.FacingYawDegrees, 0f);
            cal.ConfigureRuntimeHandover(localPos, localRot, campusHandoverPosition, campusRot);
            TryUpdateFromMapSpaceFallback();
            return true;
        }

        // ---------------------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------------------

        private void OnEnable()
        {
            TryDiscoverReferences(force: true);
            TrySubscribe();
        }

        private void OnDisable()
        {
            TryUnsubscribe();
            ResetLocalizationSession();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                ResetLocalizationSession();
            }
        }

        private void LateUpdate()
        {
            if (Time.time >= _nextDiscoveryTime)
            {
                _nextDiscoveryTime = Time.time + discoveryIntervalSeconds;
                TryDiscoverReferences(force: false);
                if (_subscribedManager == null) TrySubscribe();
            }

            if (!alwaysRunFallback && _last.Source == PoseSource.ReflectionEvent) return;
            if (Time.time < _nextFallbackTime) return;
            _nextFallbackTime = Time.time + fallbackUpdateIntervalSeconds;

            TryUpdateFromMapSpaceFallback();
        }

        // ---------------------------------------------------------------------
        // Discovery
        // ---------------------------------------------------------------------

        private void TryDiscoverReferences(bool force)
        {
            if (arCamera == null || force)
            {
                if (arCamera == null) arCamera = Camera.main;
                if (arCamera == null)
                {
                    var xr = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsInactive.Include);
                    if (xr != null) arCamera = xr.Camera;
                }
            }

            if (mapSpace == null || force)
            {
                if (mapSpace == null)
                {
                    foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                    {
                        if (t == null) continue;
                        if (t.name != "Map Space" && t.name != "MapSpace") continue;
                        if (!t.gameObject.scene.IsValid()) continue;
                        mapSpace = t;
                        break;
                    }
                }
            }

            if (mapLocalizationManager == null || force)
            {
                if (mapLocalizationManager == null)
                {
                    foreach (var m in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        if (m != null && m.GetType().Name == "MapLocalizationManager")
                        {
                            mapLocalizationManager = m;
                            break;
                        }
                    }
                }
            }

            if (calibration == null && currentBuilding != BuildingId.None)
            {
                calibration = IndoorMapCalibration.FindFor(currentBuilding, currentFloorId);
            }
        }

        // ---------------------------------------------------------------------
        // Fallback path — read camera-in-MapSpace, convert via calibration
        // ---------------------------------------------------------------------

        private void TryUpdateFromMapSpaceFallback()
        {
            if (arCamera == null || mapSpace == null) return;
            if (requireVerifiedLocalizationForFallback && !_hasVerifiedLocalization) return;

            Vector3 localPos = mapSpace.InverseTransformPoint(arCamera.transform.position);
            Quaternion localRot = Quaternion.Inverse(mapSpace.rotation) * arCamera.transform.rotation;

            var cal = ResolveCalibration();
            Vector3 campusPos;
            Quaternion campusRot;
            if (cal != null)
            {
                cal.MapLocalToCampusWorld(localPos, localRot, out campusPos, out campusRot);
            }
            else
            {
                // No calibration: pass-through (campus == map local). State machine sẽ gate trên cái này.
                campusPos = localPos;
                campusRot = localRot;
            }

            var reading = new PoseReading
            {
                MapLocalPosition = localPos,
                MapLocalRotation = localRot,
                CampusPosition = campusPos,
                CampusRotation = campusRot,
                MapId = _verifiedMapId,
                Confidence = _verifiedConfidence,
                ConfidenceAvailable = _verifiedConfidenceAvailable,
                MapIdAvailable = _verifiedMapIdAvailable,
                Timestamp = Time.timeAsDouble,
                Source = PoseSource.MapSpaceFallback,
            };

            _last = reading;
            OnPoseUpdated?.Invoke(reading);
        }

        // ---------------------------------------------------------------------
        // Reflection path — subscribe LocalizationSuccess, extract response
        // ---------------------------------------------------------------------

        private void TrySubscribe()
        {
            if (mapLocalizationManager == null) return;
            if (_subscribedManager == mapLocalizationManager) return;
            TryUnsubscribe();

            var t = mapLocalizationManager.GetType();

            // Probe các tên field event có thể có (theo string dump DLL).
            string[] candidateNames =
            {
                "LocalizationSuccessCallback",
                "OnLocalizationSuccess",
                "LocalizationSuccess",
                "onLocalizationSuccess",
            };

            FieldInfo chosenField = null;
            foreach (var name in candidateNames)
            {
                var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f == null) continue;
                if (!typeof(UnityEventBase).IsAssignableFrom(f.FieldType)) continue;
                if (TrySubscribeTyped(f) || TrySubscribeNoArg(f))
                {
                    chosenField = f;
                    break;
                }
            }

            if (chosenField == null)
            {
                if (verboseLog) Debug.Log("[MultisetPoseProvider] Không tìm thấy UnityEvent localization success — chỉ chạy fallback.");
                return;
            }

            _subscribedManager = mapLocalizationManager;
            _subscribedEventField = chosenField;
            TrySubscribeFailureEvent(t);

            // Cache response field (nếu manager expose 'last response' field).
            TryCacheResponseField(t);
            _localizedMapIdField = t.GetField(
                "localizedMapId",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        private bool TrySubscribeTyped(FieldInfo eventField)
        {
            // UnityEvent<T> có generic args; quét xem T có position/rotation/confidence không.
            var et = eventField.FieldType;
            Type genericArg = null;
            for (var cur = et; cur != null; cur = cur.BaseType)
            {
                if (cur.IsGenericType && cur.GetGenericTypeDefinition() == typeof(UnityEvent<>))
                {
                    genericArg = cur.GetGenericArguments()[0];
                    break;
                }
            }
            if (genericArg == null) return false;

            BindMembers(genericArg);
            if (!HasPoseMembers()) return false;

            var evt = eventField.GetValue(mapLocalizationManager);
            if (evt == null) return false;

            var addMethod = eventField.FieldType.GetMethod("AddListener");
            var removeMethod = eventField.FieldType.GetMethod("RemoveListener");
            if (addMethod == null) return false;

            var actionType = typeof(UnityAction<>).MakeGenericType(genericArg);
            var handler = GetType().GetMethod(nameof(OnTypedLocalizationFired), BindingFlags.Instance | BindingFlags.NonPublic);
            var typedHandler = handler.MakeGenericMethod(genericArg);
            _typedListener = Delegate.CreateDelegate(actionType, this, typedHandler);

            addMethod.Invoke(evt, new object[] { _typedListener });
            _typedAddMethod = addMethod;
            _typedRemoveMethod = removeMethod;
            _responseType = genericArg;

            if (verboseLog)
            {
                Debug.Log($"[MultisetPoseProvider] Subscribed typed {eventField.Name}<{genericArg.Name}>.");
            }
            return true;
        }

        private bool TrySubscribeNoArg(FieldInfo eventField)
        {
            if (eventField.FieldType != typeof(UnityEvent)) return false;
            var evt = eventField.GetValue(mapLocalizationManager) as UnityEvent;
            if (evt == null) return false;

            _noArgListener = OnNoArgLocalizationFired;
            evt.AddListener(_noArgListener);

            if (verboseLog) Debug.Log($"[MultisetPoseProvider] Subscribed no-arg {eventField.Name}.");
            return true;
        }

        private void TryUnsubscribe()
        {
            if (_subscribedManager == null || _subscribedEventField == null) return;

            try
            {
                var evt = _subscribedEventField.GetValue(_subscribedManager);
                if (evt is UnityEvent ue && _noArgListener != null)
                {
                    ue.RemoveListener(_noArgListener);
                }
                else if (_typedListener != null && _typedRemoveMethod != null)
                {
                    _typedRemoveMethod.Invoke(evt, new object[] { _typedListener });
                }

                if (_subscribedFailureEventField != null && _failureListener != null)
                {
                    var failureEvent =
                        _subscribedFailureEventField.GetValue(_subscribedManager) as UnityEvent;
                    failureEvent?.RemoveListener(_failureListener);
                }
            }
            catch (Exception ex)
            {
                if (verboseLog) Debug.LogWarning($"[MultisetPoseProvider] Unsubscribe lỗi: {ex.Message}");
            }

            _subscribedManager = null;
            _subscribedEventField = null;
            _subscribedFailureEventField = null;
            _noArgListener = null;
            _failureListener = null;
            _typedListener = null;
            _typedAddMethod = null;
            _typedRemoveMethod = null;
            _responseType = null;
        }

        private void TrySubscribeFailureEvent(Type managerType)
        {
            string[] candidateNames =
            {
                "LocalizationFailure",
                "OnLocalizationFailure",
                "localizationFailure",
            };

            foreach (string name in candidateNames)
            {
                FieldInfo field = managerType.GetField(
                    name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field == null || field.FieldType != typeof(UnityEvent)) continue;
                if (field.GetValue(mapLocalizationManager) is not UnityEvent evt) continue;

                _failureListener = OnLocalizationFailureFired;
                evt.AddListener(_failureListener);
                _subscribedFailureEventField = field;
                return;
            }
        }

        private void OnLocalizationFailureFired()
        {
            ResetLocalizationSession();
            if (verboseLog)
                Debug.LogWarning("[MultisetPoseProvider] LocalizationFailure → revoke indoor pose.");
        }

        // Typed listener — Unity sẽ gọi đúng method qua delegate.
        private void OnTypedLocalizationFired<T>(T response)
        {
            ApplyResponseObject(response);
        }

        private void OnNoArgLocalizationFired()
        {
            // SDK không trả response — thử đọc field 'last response' đã cache trên manager.
            if (_responseFieldOnManager != null && _subscribedManager != null)
            {
                object obj = _responseFieldOnManager.GetValue(_subscribedManager);
                if (obj != null)
                {
                    if (!HasPoseMembers()) BindMembers(obj.GetType());
                    ApplyResponseObject(obj);
                    return;
                }
            }

            // SDK hiện tại phát UnityEvent không tham số. Chỉ lấy map ID từ field thật
            // localizedMapId nếu có; tuyệt đối không giả confidence = 1.
            string mapId = null;
            if (_localizedMapIdField != null && _subscribedManager != null)
            {
                mapId = _localizedMapIdField.GetValue(_subscribedManager)?.ToString();
            }
            MarkLocalizationVerified(0f, false, mapId, !string.IsNullOrWhiteSpace(mapId));
            TryUpdateFromMapSpaceFallback();
        }

        private void ApplyResponseObject(object response)
        {
            if (response == null) return;

            Vector3 pos = ReadVector3(_respPosition, response);
            Quaternion rot = ReadQuaternion(_respRotation, response);
            bool confidenceAvailable = _respConfidence.Valid;
            float conf = ReadFloat(_respConfidence, response, 0f);
            string mapId = ReadString(_respMapId, response);
            bool mapIdAvailable = !string.IsNullOrWhiteSpace(mapId);
            MarkLocalizationVerified(
                conf,
                confidenceAvailable,
                mapId,
                mapIdAvailable);

            var cal = ResolveCalibration();
            Vector3 campusPos; Quaternion campusRot;
            if (cal != null)
            {
                cal.MapLocalToCampusWorld(pos, rot, out campusPos, out campusRot);
            }
            else
            {
                campusPos = pos;
                campusRot = rot;
            }

            _last = new PoseReading
            {
                MapLocalPosition = pos,
                MapLocalRotation = rot,
                CampusPosition = campusPos,
                CampusRotation = campusRot,
                MapId = mapId,
                Confidence = conf,
                ConfidenceAvailable = confidenceAvailable,
                MapIdAvailable = mapIdAvailable,
                Timestamp = Time.timeAsDouble,
                Source = PoseSource.ReflectionEvent,
            };
            OnPoseUpdated?.Invoke(_last);
        }

        private void MarkLocalizationVerified(
            float confidence,
            bool confidenceAvailable,
            string mapId,
            bool mapIdAvailable)
        {
            _hasVerifiedLocalization = true;
            _verifiedConfidence = Mathf.Clamp01(confidence);
            _verifiedConfidenceAvailable = confidenceAvailable;
            _verifiedMapId = mapId;
            _verifiedMapIdAvailable = mapIdAvailable;
        }

        private IndoorMapCalibration ResolveCalibration()
        {
            if (calibration != null) return calibration;
            if (currentBuilding == BuildingId.None) return null;
            calibration = IndoorMapCalibration.FindFor(currentBuilding, currentFloorId);
            return calibration;
        }

        // ---------------------------------------------------------------------
        // Reflection member binding
        // ---------------------------------------------------------------------

        private struct FieldOrProp
        {
            public FieldInfo Field;
            public PropertyInfo Prop;
            public bool Valid => Field != null || Prop != null;
            public Type MemberType => Field != null ? Field.FieldType : Prop?.PropertyType;
            public object Get(object instance) => Field != null ? Field.GetValue(instance) : Prop.GetValue(instance);
        }

        private static FieldOrProp FindMember(Type t, params string[] names)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var name in names)
            {
                var f = t.GetField(name, flags);
                if (f != null) return new FieldOrProp { Field = f };
                var p = t.GetProperty(name, flags);
                if (p != null) return new FieldOrProp { Prop = p };
            }
            // Case-insensitive scan as last resort.
            foreach (var f in t.GetFields(flags))
            {
                foreach (var n in names)
                {
                    if (string.Equals(f.Name, n, StringComparison.OrdinalIgnoreCase)) return new FieldOrProp { Field = f };
                }
            }
            foreach (var p in t.GetProperties(flags))
            {
                foreach (var n in names)
                {
                    if (string.Equals(p.Name, n, StringComparison.OrdinalIgnoreCase)) return new FieldOrProp { Prop = p };
                }
            }
            return default;
        }

        private void BindMembers(Type responseType)
        {
            _respPosition = FindMember(responseType, "position", "Position");
            _respRotation = FindMember(responseType, "rotation", "Rotation");
            _respConfidence = FindMember(responseType, "confidence", "Confidence");
            _respMapId = FindMember(responseType, "mapId", "MapId", "mapID");
        }

        private bool HasPoseMembers()
        {
            return _respPosition.Valid && _respRotation.Valid;
        }

        private void TryCacheResponseField(Type managerType)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            string[] candidates =
            {
                "lastLocalizationResult",
                "lastLocalizationResponse",
                "LastLocalizationResponse",
                "localizationResult",
                "currentLocalizationResponse",
                "localizationSuccessResponse",
            };
            foreach (var n in candidates)
            {
                var f = managerType.GetField(n, flags);
                if (f != null)
                {
                    _responseFieldOnManager = f;
                    BindMembers(f.FieldType);
                    return;
                }
            }
        }

        private static Vector3 ReadVector3(FieldOrProp m, object instance)
        {
            if (!m.Valid) return Vector3.zero;
            object v = m.Get(instance);
            if (v == null) return Vector3.zero;
            if (v is Vector3 vec) return vec;
            // try extract x/y/z fields
            var t = v.GetType();
            float x = ReadNumeric(t, v, "x") ?? 0f;
            float y = ReadNumeric(t, v, "y") ?? 0f;
            float z = ReadNumeric(t, v, "z") ?? 0f;
            return new Vector3(x, y, z);
        }

        private static Quaternion ReadQuaternion(FieldOrProp m, object instance)
        {
            if (!m.Valid) return Quaternion.identity;
            object v = m.Get(instance);
            if (v == null) return Quaternion.identity;
            if (v is Quaternion q) return q;
            if (v is Vector3 euler) return Quaternion.Euler(euler);
            var t = v.GetType();
            float? x = ReadNumeric(t, v, "x");
            float? y = ReadNumeric(t, v, "y");
            float? z = ReadNumeric(t, v, "z");
            float? w = ReadNumeric(t, v, "w");
            if (w.HasValue) return new Quaternion(x ?? 0, y ?? 0, z ?? 0, w.Value);
            return Quaternion.Euler(x ?? 0, y ?? 0, z ?? 0);
        }

        private static float ReadFloat(FieldOrProp m, object instance, float fallback)
        {
            if (!m.Valid) return fallback;
            object v = m.Get(instance);
            return v is IConvertible c ? Convert.ToSingle(c) : fallback;
        }

        private static string ReadString(FieldOrProp m, object instance)
        {
            if (!m.Valid) return null;
            return m.Get(instance)?.ToString();
        }

        private static float? ReadNumeric(Type t, object instance, string name)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var f = t.GetField(name, flags);
            if (f != null && f.GetValue(instance) is IConvertible c1) return Convert.ToSingle(c1);
            var p = t.GetProperty(name, flags);
            if (p != null && p.GetValue(instance) is IConvertible c2) return Convert.ToSingle(c2);
            return null;
        }
    }
}
