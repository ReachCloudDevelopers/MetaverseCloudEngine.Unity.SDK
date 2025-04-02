#import <Foundation/Foundation.h>
#import <ifaddrs.h>
#import <arpa/inet.h>
#import <netinet/in.h>
#import <sys/socket.h>

#define DISCOVERY_PORT 4210
#define DISCOVERY_MESSAGE "DISCOVER_ESP32"

@interface LANDevice : NSObject
@property (nonatomic, strong) NSString *name;
@property (nonatomic, strong) NSString *ipAddress;
@property (nonatomic, strong) NSDate *lastSeen;
@end

@implementation LANDevice
@end

@interface SPAPWiFiManager : NSObject
@property (nonatomic, strong) NSMutableDictionary<NSString *, LANDevice *> *discoveredDevices;
@property (nonatomic, strong) NSTimer *scanTimer;
@property (nonatomic, strong) NSInputStream *inputStream;
@property (nonatomic, strong) NSOutputStream *outputStream;
@property (nonatomic, strong) NSMutableData *incomingData;
@property (nonatomic, strong) NSString *connectedIPAddress;

+ (instancetype)sharedManager;
- (void)startScan;
@end

@implementation SPAPWiFiManager

+ (instancetype)sharedManager {
    static SPAPWiFiManager *manager = nil;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        manager = [[SPAPWiFiManager alloc] init];
    });
    return manager;
}

- (instancetype)init {
    if (self = [super init]) {
        _discoveredDevices = [NSMutableDictionary dictionary];
    }
    return self;
}

- (void)startScan {
    [self.discoveredDevices removeAllObjects];
    [self sendDiscoveryBroadcast];

    if (self.scanTimer) {
        [self.scanTimer invalidate];
    }
    self.scanTimer = [NSTimer scheduledTimerWithTimeInterval:5.0 target:self selector:@selector(sendDiscoveryBroadcast) userInfo:nil repeats:YES];
}

- (void)sendDiscoveryBroadcast {
    int sock = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP);
    if (sock < 0) {
        NSLog(@"socket failed");
        return;
    }

    int broadcastEnable = 1;
    setsockopt(sock, SOL_SOCKET, SO_BROADCAST, &broadcastEnable, sizeof(broadcastEnable));

    struct sockaddr_in broadcastAddr;
    memset(&broadcastAddr, 0, sizeof(broadcastAddr));
    broadcastAddr.sin_family = AF_INET;
    broadcastAddr.sin_port = htons(DISCOVERY_PORT);
    broadcastAddr.sin_addr.s_addr = htonl(INADDR_BROADCAST);

    sendto(sock, DISCOVERY_MESSAGE, strlen(DISCOVERY_MESSAGE), 0, (struct sockaddr *)&broadcastAddr, sizeof(broadcastAddr));

    dispatch_async(dispatch_get_global_queue(DISPATCH_QUEUE_PRIORITY_DEFAULT, 0), ^{
        char buffer[1024];
        struct sockaddr_in senderAddr;
        socklen_t addrLen = sizeof(senderAddr);
        ssize_t recvLen = recvfrom(sock, buffer, sizeof(buffer) - 1, 0, (struct sockaddr *)&senderAddr, &addrLen);
        if (recvLen > 0) {
            buffer[recvLen] = '\0';
            NSString *response = [NSString stringWithUTF8String:buffer];
            NSString *ip = [NSString stringWithUTF8String:inet_ntoa(senderAddr.sin_addr)];

            LANDevice *device = [[LANDevice alloc] init];
            device.name = response;
            device.ipAddress = ip;
            device.lastSeen = [NSDate date];

            self.discoveredDevices[ip] = device;

            NSLog(@"Discovered ESP32: %@ @ %@", response, ip);
        }

        close(sock);
    });
}

- (void)connectToIPAddress:(NSString *)ip {
    if (self.inputStream || self.outputStream) {
        [self disconnect];
    }

    CFReadStreamRef readStream;
    CFWriteStreamRef writeStream;
    CFStreamCreatePairWithSocketToHost(NULL, (__bridge CFStringRef)ip, 5000, &readStream, &writeStream);

    self.inputStream = (__bridge_transfer NSInputStream *)readStream;
    self.outputStream = (__bridge_transfer NSOutputStream *)writeStream;

    self.inputStream.delegate = (id<NSStreamDelegate>)self;
    self.outputStream.delegate = (id<NSStreamDelegate>)self;

    [self.inputStream scheduleInRunLoop:[NSRunLoop mainRunLoop] forMode:NSDefaultRunLoopMode];
    [self.outputStream scheduleInRunLoop:[NSRunLoop mainRunLoop] forMode:NSDefaultRunLoopMode];

    [self.inputStream open];
    [self.outputStream open];

    self.incomingData = [NSMutableData data];
    self.connectedIPAddress = ip;

    NSLog(@"Connecting to ESP32 at %@", ip);
}

- (void)disconnect {
    [self.inputStream close];
    [self.outputStream close];
    [self.inputStream removeFromRunLoop:[NSRunLoop mainRunLoop] forMode:NSDefaultRunLoopMode];
    [self.outputStream removeFromRunLoop:[NSRunLoop mainRunLoop] forMode:NSDefaultRunLoopMode];
    self.inputStream = nil;
    self.outputStream = nil;
    self.connectedIPAddress = nil;
    self.incomingData = nil;
    NSLog(@"Disconnected from ESP32");
}

- (void)writeData:(NSData *)data {
    if (!self.outputStream || ![self.outputStream hasSpaceAvailable]) return;
    [self.outputStream write:data.bytes maxLength:data.length];
}

- (NSData *)readDataUpToLength:(NSUInteger)length {
    NSUInteger available = self.incomingData.length;
    if (available == 0) return nil;

    NSUInteger toCopy = MIN(length, available);
    NSData *data = [self.incomingData subdataWithRange:NSMakeRange(0, toCopy)];
    [self.incomingData replaceBytesInRange:NSMakeRange(0, toCopy) withBytes:NULL length:0];
    return data;
}

#pragma mark - NSStreamDelegate

- (void)stream:(NSStream *)aStream handleEvent:(NSStreamEvent)eventCode {
    if (eventCode == NSStreamEventHasBytesAvailable && aStream == self.inputStream) {
        uint8_t buffer[1024];
        NSInteger bytesRead = [self.inputStream read:buffer maxLength:sizeof(buffer)];
        if (bytesRead > 0) {
            [self.incomingData appendBytes:buffer length:bytesRead];
        }
    }
}

@end

// ---------------------------------------------------------------------
// C-exported functions for Unity to call.
// These functions are linked with the DllImport calls from your C# layer.
#ifdef __cplusplus
extern "C" {
#endif

void ios_startScan(void) {
    SPAPWiFiManager *manager = [SPAPWiFiManager sharedManager];
    [manager startScan];
    NSLog(@"ios_startScan: Wi-Fi scan started.");
}

int ios_connectToDevice(const char *serialNumber) {
    NSString *ip = [NSString stringWithUTF8String:serialNumber];
    SPAPWiFiManager *manager = [SPAPWiFiManager sharedManager];
    if (manager.discoveredDevices[ip]) {
        [manager connectToIPAddress:ip];
        return 0;
    }
    NSLog(@"ios_connectToDevice: No matching device for IP %@", ip);
    return -1;
}

void ios_disconnect(void) {
    [[SPAPWiFiManager sharedManager] disconnect];
}

int ios_writeData(const uint8_t *data, int length) {
    SPAPWiFiManager *manager = [SPAPWiFiManager sharedManager];
    if (!manager.outputStream || ![manager.outputStream hasSpaceAvailable]) return -1;
    NSData *toWrite = [NSData dataWithBytes:data length:length];
    [manager writeData:toWrite];
    return length;
}

int ios_readData(uint8_t *buffer, int bufferSize) {
    SPAPWiFiManager *manager = [SPAPWiFiManager sharedManager];
    NSData *read = [manager readDataUpToLength:bufferSize];
    if (!read) return 0;
    memcpy(buffer, read.bytes, read.length);
    return (int)read.length;
}

int ios_spapDeviceListAvailable(void) {
    SPAPWiFiManager *manager = [SPAPWiFiManager sharedManager];
    return (int)[manager.discoveredDevices count];
}

int ios_spapDeviceList(int deviceNum, char *deviceInfo, int bufferSize) {
    SPAPWiFiManager *manager = [SPAPWiFiManager sharedManager];
    NSArray *devices = [[manager.discoveredDevices allValues] sortedArrayUsingComparator:^NSComparisonResult(LANDevice *obj1, LANDevice *obj2) {
        return [obj1.lastSeen compare:obj2.lastSeen];
    }];

    if (deviceNum >= [devices count]) return -1;

    LANDevice *dev = devices[deviceNum];
    NSString *vendor = @"ESP32";
    NSString *product = dev.name ?: @"Unnamed";
    NSString *serialNumber = dev.ipAddress;
    NSString *portName = @"";

    NSString *info = [NSString stringWithFormat:@"%@,%@,%@,%@", vendor, product, serialNumber, portName];
    const char *cInfo = [info UTF8String];
    strncpy(deviceInfo, cInfo, bufferSize);
    deviceInfo[bufferSize - 1] = '\0';
    return 2;
}

#ifdef __cplusplus
}
#endif

