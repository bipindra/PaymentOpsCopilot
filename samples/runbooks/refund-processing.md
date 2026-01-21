# Refund Processing Guide

## Overview
Refunds must be processed accurately and quickly to maintain customer trust.

## Refund Types
1. **Full Refund**: Return entire transaction amount
2. **Partial Refund**: Return portion of transaction
3. **Void**: Cancel before settlement (no fee)
4. **Chargeback**: Disputed by cardholder

## Processing Time
- Standard refund: 5-10 business days
- Instant refund: Available for eligible transactions
- Void: Immediate (if before settlement)

## Refund Rules
- Can only refund up to original transaction amount
- Must refund to same payment method
- Partial refunds limited to 5 per transaction
- Refunds expire after 180 days

## Error Handling
### Common Errors
- **Insufficient funds**: Original transaction already refunded
- **Expired card**: Contact customer for new payment method
- **Invalid transaction ID**: Verify transaction exists and is refundable

### Recovery
1. Verify transaction status
2. Check refund eligibility
3. If error persists, escalate to payment processor support
4. Document in refund log

## Reconciliation
- Match refunds to original transactions daily
- Verify amounts match
- Flag discrepancies for review
- Generate refund report weekly
