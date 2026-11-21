#import <Foundation/Foundation.h>

// PerfGovernor: the device's thermal state (0 nominal · 1 fair · 2 serious · 3 critical).
extern "C" int _HoldoutThermalState()
{
    return (int)[[NSProcessInfo processInfo] thermalState];
}

extern "C" bool _HoldoutLowPowerMode()
{
    return [[NSProcessInfo processInfo] isLowPowerModeEnabled];
}
