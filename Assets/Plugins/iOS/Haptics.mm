#import <UIKit/UIKit.h>

extern "C" void _OZT_Haptic(int style)
{
    UIImpactFeedbackStyle s = style == 0 ? UIImpactFeedbackStyleLight
                            : style == 1 ? UIImpactFeedbackStyleMedium
                                         : UIImpactFeedbackStyleHeavy;
    UIImpactFeedbackGenerator *g = [[UIImpactFeedbackGenerator alloc] initWithStyle:s];
    [g impactOccurred];
}
