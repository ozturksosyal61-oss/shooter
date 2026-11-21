#import <Foundation/Foundation.h>
#import <GameKit/GameKit.h>

// CloudSync: Game Center identity verification signature → Unity Authentication (SignInWithAppleGameCenterAsync).
// Calls back with a JSON object: publicKeyUrl, signature (base64), salt (base64), timestamp, teamPlayerId — or error.
typedef void (*HoldoutGcIdCallback)(const char *json);

static NSString *HoldoutJson(NSDictionary *d)
{
    NSData *data = [NSJSONSerialization dataWithJSONObject:d options:0 error:nil];
    return data ? [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding] : @"{\"error\":\"json\"}";
}

extern "C" void _HoldoutGameCenterIdentity(HoldoutGcIdCallback cb)
{
    GKLocalPlayer *p = [GKLocalPlayer localPlayer];
    if (!p.isAuthenticated) { cb([HoldoutJson(@{ @"error": @"not authenticated" }) UTF8String]); return; }
    void (^handler)(NSURL *, NSData *, NSData *, uint64_t, NSError *) = ^(NSURL *url, NSData *sig, NSData *salt, uint64_t ts, NSError *err)
    {
        NSString *json;
        if (err || !url || !sig || !salt)
            json = HoldoutJson(@{ @"error": err ? err.localizedDescription : @"empty" });
        else
            json = HoldoutJson(@{
                @"publicKeyUrl": url.absoluteString,
                @"signature": [sig base64EncodedStringWithOptions:0],
                @"salt": [salt base64EncodedStringWithOptions:0],
                @"timestamp": [NSString stringWithFormat:@"%llu", ts],
                @"teamPlayerId": p.teamPlayerID ?: @"",
            });
        dispatch_async(dispatch_get_main_queue(), ^{ cb([json UTF8String]); });
    };
    if (@available(iOS 13.5, *)) [p fetchItemsForIdentityVerificationSignature:handler];
    else cb([HoldoutJson(@{ @"error": @"iOS < 13.5" }) UTF8String]);
}
