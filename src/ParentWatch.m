#import <Foundation/Foundation.h>
#include <unistd.h>
#include <stdlib.h>
// Exit the media helper when its owning app closes or crashes. This file is
// compiled into the bundled adapter; ordinary upstream get/send calls are unaffected.
__attribute__((constructor)) static void vk03_parent_watch(void) {
    if (!getenv("VK03_PARENT_PIPE")) return;
    dispatch_async(dispatch_get_global_queue(QOS_CLASS_UTILITY,0), ^{
        char byte;
        while (read(STDIN_FILENO,&byte,1)>0) {}
        _exit(0);
    });
}
