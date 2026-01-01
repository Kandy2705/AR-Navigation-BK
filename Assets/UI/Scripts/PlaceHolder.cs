using UnityEngine.UIElements;
using UnityEngine;

// 1. Dùng Attribute này để đăng ký control (Thay cho UxmlFactory)
[UxmlElement]
public partial class PlaceHolder : TextField
{
    private Label _placeholderLabel;
    private const string PlaceholderClass = "placeholder-label";
    private const string HiddenClass = "placeholder-hidden";
    [UxmlAttribute("placeholder-text")] 
    public string Placeholder
    {
        get => _placeholderLabel?.text ?? "";
        set
        {
            if (_placeholderLabel != null)
            {
                _placeholderLabel.text = value;
                // Cập nhật lại trạng thái hiển thị ngay khi đổi text trong Editor
                UpdatePlaceholderVisibility(this.value); 
            }
        }
    }

    // Constructor mặc định
    public PlaceHolder()
    {
        // Tạo Label
        _placeholderLabel = new Label();
        _placeholderLabel.AddToClassList(PlaceholderClass);
        _placeholderLabel.pickingMode = PickingMode.Ignore;
        Add(_placeholderLabel);

        // Đăng ký sự kiện
        this.RegisterValueChangedCallback(evt => UpdatePlaceholderVisibility(evt.newValue));

        // Đảm bảo chạy 1 lần lúc khởi tạo
        this.schedule.Execute(() => UpdatePlaceholderVisibility(this.value));
    }

    private void UpdatePlaceholderVisibility(string text)
    {
        if (string.IsNullOrEmpty(text))
            _placeholderLabel.RemoveFromClassList(HiddenClass);
        else
            _placeholderLabel.AddToClassList(HiddenClass);
    }
}