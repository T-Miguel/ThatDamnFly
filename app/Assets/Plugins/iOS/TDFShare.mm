// Native iOS sharing (UIActivityViewController) and light vibration. Compiled by Xcode; no third-party SDKs.
#import <UIKit/UIKit.h>
#import <AudioToolbox/AudioToolbox.h>

extern "C" {
    void TDF_ShareIOS(const char* text, const char* url) {
        NSString* t = [NSString stringWithUTF8String:text ?: ""];
        NSString* u = [NSString stringWithUTF8String:url ?: ""];
        NSMutableArray* items = [NSMutableArray array];
        [items addObject:t];
        if (u.length > 0) { NSURL* nsurl = [NSURL URLWithString:u]; if (nsurl) [items addObject:nsurl]; }
        UIActivityViewController* vc = [[UIActivityViewController alloc] initWithActivityItems:items applicationActivities:nil];
        UIViewController* root = [UIApplication sharedApplication].keyWindow.rootViewController;
        if (vc.popoverPresentationController) { vc.popoverPresentationController.sourceView = root.view; vc.popoverPresentationController.sourceRect = CGRectMake(root.view.bounds.size.width / 2, root.view.bounds.size.height - 40, 1, 1); }
        [root presentViewController:vc animated:YES completion:nil];
    }
    void TDF_ShareImageIOS(const char* path, const char* text, const char* url) {
        NSString* p = [NSString stringWithUTF8String:path ?: ""];
        NSString* t = [NSString stringWithUTF8String:text ?: ""];
        NSString* u = [NSString stringWithUTF8String:url ?: ""];
        NSMutableArray* items = [NSMutableArray array];
        UIImage* img = [UIImage imageWithContentsOfFile:p]; if (img) [items addObject:img];
        [items addObject:[NSString stringWithFormat:@"%@ %@", t, u]];
        UIActivityViewController* vc = [[UIActivityViewController alloc] initWithActivityItems:items applicationActivities:nil];
        UIViewController* root = [UIApplication sharedApplication].keyWindow.rootViewController;
        if (vc.popoverPresentationController) { vc.popoverPresentationController.sourceView = root.view; vc.popoverPresentationController.sourceRect = CGRectMake(root.view.bounds.size.width / 2, root.view.bounds.size.height - 40, 1, 1); }
        [root presentViewController:vc animated:YES completion:nil];
    }
    void TDF_VibrateIOS(int strong) {
        if (@available(iOS 10.0, *)) {
            UIImpactFeedbackGenerator* g = [[UIImpactFeedbackGenerator alloc] initWithStyle:(strong ? UIImpactFeedbackStyleHeavy : UIImpactFeedbackStyleLight)];
            [g impactOccurred];
        } else { AudioServicesPlaySystemSound(kSystemSoundID_Vibrate); }
    }
}
