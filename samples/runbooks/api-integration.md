# Payment API Integration Guide

## Overview
This guide covers integrating with our payment processing API.

## Authentication
All API requests require authentication via API key in header:
```
Authorization: Bearer <api_key>
```

## Endpoints
### Create Payment
```
POST /api/v1/payments
Content-Type: application/json

{
  "amount": 1000,
  "currency": "USD",
  "payment_method": {
    "type": "card",
    "number": "4111111111111111",
    "expiry_month": 12,
    "expiry_year": 2025,
    "cvv": "123"
  }
}
```

### Get Payment Status
```
GET /api/v1/payments/{payment_id}
```

### Refund Payment
```
POST /api/v1/payments/{payment_id}/refund
{
  "amount": 500  // Optional, omit for full refund
}
```

## Error Handling
All errors return standard format:
```json
{
  "error": {
    "code": "INSUFFICIENT_FUNDS",
    "message": "Card has insufficient funds",
    "details": {}
  }
}
```

## Rate Limits
- 100 requests per second per API key
- 10,000 requests per day per API key
- Exceeding limits returns 429 Too Many Requests

## Webhooks
Configure webhook URL to receive payment events:
- payment.succeeded
- payment.failed
- payment.refunded

## Testing
Use test API key for sandbox environment:
- Test cards: 4242 4242 4242 4242 (success)
- Test cards: 4000 0000 0000 0002 (declined)
