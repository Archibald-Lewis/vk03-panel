#import <Foundation/Foundation.h>
void VKReadMedia(void (^ _Nonnull completion)(NSDictionary * _Nullable, BOOL));
BOOL VKSendMediaCommand(int command);
BOOL VKCanLockScreen(void);
BOOL VKLockScreen(void);
