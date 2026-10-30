#import "SystemBridge.h"
#import <dlfcn.h>

typedef void (*MediaInfoFunction)(dispatch_queue_t,void (^)(NSDictionary *));
typedef void (*MediaPlayingFunction)(dispatch_queue_t,void (^)(bool));
typedef bool (*MediaCommandFunction)(int,id);
typedef void (*LockFunction)(void);
static MediaInfoFunction infoFunction;
static MediaPlayingFunction playingFunction;
static MediaCommandFunction commandFunction;
static LockFunction lockFunction;

static void loadSystemFunctions(void) {
    static dispatch_once_t once;
    dispatch_once(&once, ^{
        void *media=dlopen("/System/Library/PrivateFrameworks/MediaRemote.framework/MediaRemote",RTLD_NOW|RTLD_LOCAL);
        if(media) {
            infoFunction=(MediaInfoFunction)dlsym(media,"MRMediaRemoteGetNowPlayingInfo");
            playingFunction=(MediaPlayingFunction)dlsym(media,"MRMediaRemoteGetNowPlayingApplicationIsPlaying");
            commandFunction=(MediaCommandFunction)dlsym(media,"MRMediaRemoteSendCommand");
        }
        void *login=dlopen("/System/Library/PrivateFrameworks/login.framework/Versions/A/login",RTLD_NOW|RTLD_LOCAL);
        if(login) lockFunction=(LockFunction)dlsym(login,"SACLockScreenImmediate");
    });
}
void VKReadMedia(void (^completion)(NSDictionary *,BOOL)) {
    loadSystemFunctions();
    if(!infoFunction || !playingFunction) { completion(nil,NO);return; }
    __block NSDictionary *information=nil;
    __block BOOL playing=NO;
    dispatch_group_t group=dispatch_group_create();
    dispatch_group_enter(group);
    infoFunction(dispatch_get_main_queue(),^(NSDictionary *value) { information=value;dispatch_group_leave(group); });
    dispatch_group_enter(group);
    playingFunction(dispatch_get_main_queue(),^(bool value) { playing=value;dispatch_group_leave(group); });
    dispatch_group_notify(group,dispatch_get_main_queue(),^{ completion(information,playing); });
}
BOOL VKSendMediaCommand(int command) {
    loadSystemFunctions();
    // Only playback controls are exposed to the touch UI.
    if(command!=2 && command!=4 && command!=5) return NO;
    return commandFunction ? commandFunction(command,nil) : NO;
}
BOOL VKCanLockScreen(void) { loadSystemFunctions();return lockFunction!=NULL; }
BOOL VKLockScreen(void) { loadSystemFunctions();if(!lockFunction)return NO;lockFunction();return YES; }
