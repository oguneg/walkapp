// Step history bridge for StepHistory.cs (iOS).
// CMPedometer keeps ~7 days of step data from the motion coprocessor, so we can ask
// "how many steps between A and B" for any window, including while the game was closed.
// Requires NSMotionUsageDescription in Info.plist (Input System settings > iOS > Motion Usage adds it).

#import <Foundation/Foundation.h>
#import <CoreMotion/CoreMotion.h>

typedef void (*WalkappPedometerCallback)(int requestId, long long steps, int ok);

static CMPedometer* s_WalkappPedometer = nil;

extern "C"
{
    int _WalkappPedometerIsAvailable()
    {
        return [CMPedometer isStepCountingAvailable] ? 1 : 0;
    }

    void _WalkappPedometerQuery(double fromUnix, double toUnix, int requestId, WalkappPedometerCallback callback)
    {
        if (callback == NULL)
            return;

        if (![CMPedometer isStepCountingAvailable])
        {
            callback(requestId, 0, 0);
            return;
        }

        if (s_WalkappPedometer == nil)
            s_WalkappPedometer = [[CMPedometer alloc] init];

        NSDate* from = [NSDate dateWithTimeIntervalSince1970:fromUnix];
        NSDate* to = [NSDate dateWithTimeIntervalSince1970:toUnix];

        [s_WalkappPedometer queryPedometerDataFromDate:from toDate:to withHandler:^(CMPedometerData* data, NSError* error)
        {
            long long steps = 0;
            int ok = 0;
            if (error == nil && data != nil)
            {
                steps = [data.numberOfSteps longLongValue];
                ok = 1;
            }
            else if (error != nil)
            {
                NSLog(@"WalkappPedometer - query failed: %@", [error localizedDescription]);
            }

            // CMPedometer answers on a background queue; Unity scripting runs on the main thread.
            dispatch_async(dispatch_get_main_queue(), ^{
                callback(requestId, steps, ok);
            });
        }];
    }
}
