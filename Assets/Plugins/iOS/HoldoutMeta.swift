import Foundation
import FBSDKCoreKit

// Meta (Facebook) SDK bridge for MetaEvents.cs — app events only (no Facebook Login).
// Purchases are logged automatically by the SDK ("Log in-app events automatically" on in Meta) — never by hand.
// Advertiser ID collection follows the App Tracking Transparency answer; FBSDK 17+ also reads the ATT status itself,
// so a refused prompt means no advertiser tracking.

@_cdecl("_HoldoutMetaInit")
public func holdoutMetaInit(_ trackingAllowed: Int32) {
    Settings.shared.isAutoLogAppEventsEnabled = true
    Settings.shared.isAdvertiserIDCollectionEnabled = trackingAllowed != 0
    ApplicationDelegate.shared.initializeSDK()
    AppEvents.shared.activateApp()
}

@_cdecl("_HoldoutMetaActivate")
public func holdoutMetaActivate() {
    AppEvents.shared.activateApp()
}

/// name: event name; paramsJson: flat JSON object of string / number values (or empty).
@_cdecl("_HoldoutMetaLog")
public func holdoutMetaLog(_ name: UnsafePointer<CChar>, _ paramsJson: UnsafePointer<CChar>?) {
    let eventName = AppEvents.Name(String(cString: name))
    var params: [AppEvents.ParameterName: Any] = [:]
    if let p = paramsJson,
       let data = String(cString: p).data(using: .utf8),
       let obj = try? JSONSerialization.jsonObject(with: data) as? [String: Any] {
        for (k, v) in obj { params[AppEvents.ParameterName(k)] = v }
    }
    AppEvents.shared.logEvent(eventName, parameters: params.isEmpty ? nil : params)
}
