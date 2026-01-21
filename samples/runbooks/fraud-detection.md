# Fraud Detection and Prevention

## Overview
Protect against fraudulent transactions while minimizing false positives.

## Fraud Signals
1. **Velocity Checks**
   - Multiple transactions in short time
   - Transactions from different locations rapidly
   - High-value transactions in quick succession

2. **Card Testing**
   - Sequential card numbers
   - Multiple small transactions
   - High decline rate from same IP

3. **Account Takeover**
   - Login from new device/location
   - Unusual transaction patterns
   - Password reset followed by purchase

## Rules Engine
### Level 1: Automatic Approval
- Transaction < $50
- Known customer, good history
- Matches typical spending pattern

### Level 2: Review Queue
- Transaction $50-$500
- New customer
- Slight deviation from pattern

### Level 3: Manual Review
- Transaction > $500
- High-risk indicators present
- Multiple failed verification attempts

## Response Actions
- **Approve**: Process normally
- **Review**: Hold for manual review (24 hours)
- **Decline**: Reject transaction
- **Challenge**: Request additional verification

## Tuning Rules
- Review false positive rate monthly
- Adjust thresholds based on fraud patterns
- A/B test rule changes
- Monitor chargeback rate
