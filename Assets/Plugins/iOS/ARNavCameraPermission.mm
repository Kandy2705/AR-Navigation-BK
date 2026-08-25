#import <AVFoundation/AVFoundation.h>

extern "C"
{
    int ARNavCameraAuthorizationStatus(void)
    {
        AVAuthorizationStatus status =
            [AVCaptureDevice authorizationStatusForMediaType:AVMediaTypeVideo];

        switch (status)
        {
            case AVAuthorizationStatusNotDetermined:
                return 0;
            case AVAuthorizationStatusRestricted:
                return 1;
            case AVAuthorizationStatusDenied:
                return 2;
            case AVAuthorizationStatusAuthorized:
                return 3;
        }

        return 1;
    }

    void ARNavRequestCameraAuthorization(void)
    {
        dispatch_async(dispatch_get_main_queue(), ^{
            [AVCaptureDevice requestAccessForMediaType:AVMediaTypeVideo
                                      completionHandler:^(__unused BOOL granted) {
                                      }];
        });
    }
}
