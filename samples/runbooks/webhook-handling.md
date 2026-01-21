# Webhook Handling Guide

## Overview
Webhooks are critical for real-time payment event notifications. This guide covers best practices for handling webhooks reliably.

## Idempotency
All webhook handlers MUST be idempotent. Use the webhook ID as a deduplication key.

### Implementation
1. Store incoming webhook ID in a distributed cache (Redis) with TTL of 24 hours
2. Before processing, check if webhook ID exists
3. If exists, return 200 OK without processing
4. If not exists, process and store ID

### Example Code
```csharp
var cacheKey = $"webhook:{webhookId}";
if (await redis.ExistsAsync(cacheKey))
{
    return Ok(); // Already processed
}
await redis.SetAsync(cacheKey, "1", TimeSpan.FromHours(24));
// Process webhook...
```

## Retry Logic
Webhook delivery uses exponential backoff:
- Initial retry: 1 second
- Max retry: 5 minutes
- Max attempts: 10

## Duplicate Detection
If duplicate webhooks are received:
1. Check idempotency cache first
2. Verify signature matches
3. Compare payload hash
4. If all checks pass, process normally

## Error Handling
- Always return 200 OK if webhook was received (even if processing fails)
- Log errors for manual review
- Use dead-letter queue for persistent failures
