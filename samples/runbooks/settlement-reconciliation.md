# Settlement and Reconciliation

## Overview
Daily reconciliation ensures all transactions are properly settled and funds are accounted for.

## Settlement Process
1. **Batch Close**: Close batch at end of day (11:59 PM)
2. **Processor Settlement**: Processor settles within 24-48 hours
3. **Fund Transfer**: Funds arrive in bank account 2-3 business days
4. **Reconciliation**: Match transactions to bank deposits

## Reconciliation Steps
1. Export transaction report from system
2. Export settlement file from processor
3. Match by transaction ID and amount
4. Flag discrepancies:
   - Missing transactions
   - Amount mismatches
   - Duplicate settlements

## Common Issues
### Missing Transactions
- Check if transaction was voided
- Verify transaction reached processor
- Check for network timeouts

### Amount Mismatches
- Verify fees are calculated correctly
- Check for partial refunds
- Confirm currency conversion rates

### Duplicate Settlements
- Check for duplicate batch closes
- Verify transaction wasn't processed twice
- Review idempotency logs

## Reporting
Generate daily reconciliation report with:
- Total transactions
- Total settled amount
- Discrepancies count
- Action items

## Escalation
If discrepancies > 0.1% of daily volume:
1. Pause new transactions (if critical)
2. Contact processor support
3. Review transaction logs
4. Document root cause
