@interface SPAPWiFiManager ()
@property (nonatomic, strong) NSInputStream *inputStream;
@property (nonatomic, strong) NSOutputStream *outputStream;
@property (nonatomic, strong) NSMutableData *incomingData;
@property (nonatomic, strong) NSString *connectedIPAddress;
@end

@implementation SPAPWiFiManager

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
