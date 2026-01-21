# Payment Retry Strategy

## Overview
Retry failed payments intelligently to maximize success rate while avoiding customer friction.

## Retry Rules
- Never retry hard declines (insufficient funds, card expired)
- Always retry soft declines (network timeout, temporary failure)
- Retry hard declines only if customer explicitly requests

## Retry Schedule
1. First retry: 1 hour after initial failure
2. Second retry: 6 hours after first retry
3. Third retry: 24 hours after second retry
4. Maximum: 3 retries total

## Decline Code Mapping
### Hard Declines (No Retry)
- 51: Insufficient funds
- 54: Expired card
- 57: Transaction not permitted
- 61: Exceeds withdrawal limit

### Soft Declines (Retry)
- 05: Do not honor
- 14: Invalid card number
- 41: Lost card
- 43: Stolen card
- 96: System malfunction

## Implementation
```csharp
public bool ShouldRetry(DeclineCode code, int attemptNumber)
{
    if (IsHardDecline(code)) return false;
    if (attemptNumber >= 3) return false;
    return true;
}
```

## Customer Communication
- Send email after first failure
- Send SMS before final retry attempt
- Provide clear reason for failure
- Offer alternative payment methods
