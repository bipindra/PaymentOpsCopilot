# Wire Transfer Handling Guide

## Overview
Wire transfers are high-value, irreversible electronic payments that move funds directly between bank accounts. This guide covers the complete lifecycle of wire transfer processing, from initiation to settlement and reconciliation.

## Wire Transfer Types

### Outgoing Wires
- **Domestic Wires (ACH/Fedwire)**: Processed through Federal Reserve, typically same-day or next-day settlement
- **International Wires (SWIFT)**: Cross-border transfers using SWIFT network, 1-5 business days
- **Priority Wires**: Expedited processing with higher fees, same-day settlement

### Incoming Wires
- **Customer Deposits**: Funds received from external parties
- **Merchant Settlements**: Regular payouts from payment processors
- **Reconciliation Wires**: Matching and verifying incoming transfers

## Processing Workflow

### Step 1: Initiation
1. **Validate Request**
   - Verify customer identity (KYC checks)
   - Confirm account ownership
   - Check available balance or credit limit
   - Validate recipient bank details (routing number, SWIFT code, account number)

2. **Compliance Checks**
   - Screen against OFAC sanctions list
   - Verify transaction amount within limits
   - Check for suspicious activity patterns
   - Validate purpose of payment (required for international wires)

3. **Fee Calculation**
   - Domestic: $15-30 per wire
   - International: $35-50 per wire
   - Priority: Additional $25-50 fee
   - Verify customer has sufficient funds including fees

### Step 2: Authorization
1. **Multi-Factor Authentication**
   - Require 2FA for wires over $10,000
   - Verify authorization code or biometric confirmation
   - Log all authorization attempts

2. **Approval Workflow**
   - Wires under $50,000: Auto-approved if all checks pass
   - Wires $50,000-$250,000: Require manager approval
   - Wires over $250,000: Require senior manager + compliance review
   - International wires: Always require additional verification

3. **Hold Periods**
   - New accounts: 7-day hold on first wire
   - High-risk accounts: 3-day hold on all wires
   - Large amounts: 1-day hold for review

### Step 3: Processing
1. **Format Wire Instructions**
   ```
   Beneficiary Name: [Name]
   Beneficiary Account: [Account Number]
   Beneficiary Bank: [Bank Name]
   SWIFT/BIC: [Code]
   Routing Number: [For domestic]
   Amount: [Currency] [Amount]
   Reference: [Payment reference]
   Purpose: [Payment purpose code]
   ```

2. **Submit to Bank**
   - Domestic: Submit via Fedwire or ACH network
   - International: Submit via SWIFT network
   - Priority: Use expedited routing
   - Obtain confirmation number and tracking ID

3. **Status Tracking**
   - Pending: Submitted, awaiting bank confirmation
   - Processing: Bank has accepted, in transit
   - Completed: Funds delivered to recipient
   - Failed: Rejected by bank or network
   - Returned: Funds returned to sender

### Step 4: Settlement
1. **Debit Customer Account**
   - Deduct wire amount + fees immediately
   - Update account balance
   - Generate transaction record

2. **Update Ledger**
   - Record in general ledger
   - Update wire transfer register
   - Create audit trail entry

3. **Notification**
   - Send confirmation email to customer
   - Update transaction status in customer portal
   - Generate receipt with tracking number

## Compliance and Regulations

### Regulatory Requirements
- **Bank Secrecy Act (BSA)**: Report wires over $10,000
- **OFAC Screening**: Check all parties against sanctions list
- **AML Checks**: Monitor for money laundering patterns
- **KYC Verification**: Know Your Customer requirements

### Reporting Obligations
1. **Currency Transaction Reports (CTR)**
   - Required for wires over $10,000
   - File within 15 days of transaction
   - Include all parties and purpose

2. **Suspicious Activity Reports (SAR)**
   - File if transaction appears suspicious
   - Report within 30 days of detection
   - Maintain confidentiality

3. **International Wire Reporting**
   - Report all international wires over $3,000
   - Include sender and recipient information
   - Track purpose and destination country

### Record Keeping
- Retain all wire transfer records for 5 years
- Store authorization documents securely
- Maintain audit trail of all approvals
- Document compliance checks performed

## Error Handling

### Common Errors

#### Insufficient Funds
**Symptom**: Wire rejected due to insufficient balance
**Resolution**:
1. Verify account balance at time of request
2. Check for pending transactions
3. Contact customer to add funds
4. Retry wire after funds available
5. Document in wire log

#### Invalid Bank Details
**Symptom**: Wire rejected by receiving bank
**Resolution**:
1. Verify routing number/SWIFT code
2. Confirm account number format
3. Check beneficiary name matches account
4. Contact customer for corrected information
5. Resubmit with correct details

#### Network Failures
**Symptom**: Wire stuck in processing state
**Resolution**:
1. Check network status with bank
2. Verify wire was submitted successfully
3. Contact bank support with tracking ID
4. Monitor for status updates
5. Escalate if no update after 24 hours

#### Compliance Rejection
**Symptom**: Wire blocked by compliance checks
**Resolution**:
1. Review OFAC/AML flags
2. Verify customer identity
3. Request additional documentation
4. Escalate to compliance team
5. Document decision and rationale

### Error Recovery Process
1. **Immediate Actions**
   - Log error with full details
   - Notify customer of issue
   - Place hold on funds if needed
   - Create support ticket

2. **Investigation**
   - Review transaction details
   - Check system logs
   - Verify compliance checks
   - Contact bank if needed

3. **Resolution**
   - Fix issue if possible
   - Retry wire if appropriate
   - Refund customer if wire cannot proceed
   - Update customer on status

4. **Post-Resolution**
   - Document root cause
   - Update procedures if needed
   - Review for process improvements
   - Close support ticket

## Reconciliation

### Daily Reconciliation
1. **Match Incoming Wires**
   - Compare bank statement to internal records
   - Match by amount, date, and reference
   - Identify unmatched items
   - Investigate discrepancies

2. **Verify Outgoing Wires**
   - Confirm all submitted wires processed
   - Check for failed or returned wires
   - Verify fees charged correctly
   - Match confirmations to records

3. **Balance Verification**
   - Reconcile wire transfer account
   - Verify fees collected
   - Check for duplicate transactions
   - Confirm all holds released

### Weekly Reconciliation
1. **Volume Analysis**
   - Count total wires processed
   - Calculate total amounts
   - Compare to previous periods
   - Identify trends

2. **Fee Reconciliation**
   - Verify all fees charged
   - Check fee calculations
   - Reconcile fee revenue
   - Identify missing fees

3. **Compliance Review**
   - Verify all required reports filed
   - Check OFAC screening logs
   - Review SAR filings
   - Confirm record retention

### Monthly Reconciliation
1. **Financial Reconciliation**
   - Reconcile wire transfer GL account
   - Verify settlement amounts
   - Check for outstanding items
   - Prepare reconciliation report

2. **Audit Trail Review**
   - Verify all wires have audit trail
   - Check authorization records
   - Confirm compliance checks performed
   - Review exception handling

## Security Considerations

### Authentication
- Require strong passwords for wire access
- Implement 2FA for all wire operations
- Use hardware tokens for high-value wires
- Log all authentication attempts

### Authorization
- Implement role-based access control
- Require dual approval for large wires
- Limit wire amounts per user/role
- Monitor for unauthorized access

### Data Protection
- Encrypt wire data in transit and at rest
- Mask sensitive information in logs
- Secure storage of bank credentials
- Regular security audits

### Fraud Prevention
- Monitor for unusual patterns
- Set velocity limits per customer
- Flag high-risk destinations
- Require additional verification for new recipients

## Common Issues and Troubleshooting

### Wire Stuck in Pending
**Possible Causes**:
- Bank network delay
- Missing information
- Compliance hold
- System processing delay

**Troubleshooting Steps**:
1. Check wire status in bank system
2. Verify all required fields provided
3. Check for compliance flags
4. Review system logs for errors
5. Contact bank support with tracking ID

### Duplicate Wire Submission
**Detection**:
- Check for duplicate reference numbers
- Compare amount and recipient
- Review submission timestamps
- Check customer transaction history

**Resolution**:
- Cancel duplicate if not yet processed
- Monitor for duplicate processing
- Refund if duplicate completed
- Update procedures to prevent recurrence

### Missing Wire Confirmation
**Actions**:
1. Check spam/junk folders
2. Verify email address on file
3. Resend confirmation manually
4. Update customer contact information
5. Review email delivery logs

### Incorrect Amount Processed
**Resolution**:
1. Verify original request amount
2. Check for fee calculation errors
3. Review bank confirmation
4. Process adjustment if needed
5. Notify customer of correction

## Best Practices

### Customer Communication
- Send immediate confirmation upon submission
- Provide tracking number for reference
- Update status changes promptly
- Proactively notify of delays or issues
- Maintain clear communication channels

### Process Optimization
- Automate routine compliance checks
- Use templates for common wire types
- Implement batch processing where possible
- Streamline approval workflows
- Reduce manual data entry

### Risk Management
- Set appropriate limits per customer
- Monitor for suspicious patterns
- Maintain up-to-date compliance lists
- Regular training on fraud detection
- Review and update procedures quarterly

### Documentation
- Document all exceptions and approvals
- Maintain complete audit trail
- Keep customer communication records
- Store compliance check results
- Archive historical wire data

## Monitoring and Alerts

### Key Metrics to Monitor
- Wire processing time (target: < 2 hours)
- Error rate (target: < 1%)
- Compliance rejection rate
- Customer satisfaction scores
- Failed wire recovery time

### Alert Thresholds
- Wire pending > 4 hours: Alert operations team
- Error rate > 2%: Alert management
- Compliance rejection > 5%: Alert compliance team
- Failed wire > 24 hours: Escalate to senior management

### Daily Checks
- Review pending wires
- Check for failed wires
- Verify compliance screenings
- Monitor system health
- Review customer inquiries

## Escalation Procedures

### Level 1: Operations Team
- Handle routine wire processing
- Resolve standard errors
- Answer customer inquiries
- Process refunds < $10,000

### Level 2: Senior Operations
- Approve wires $50,000-$250,000
- Resolve complex errors
- Handle escalated customer issues
- Process refunds $10,000-$50,000

### Level 3: Management
- Approve wires > $250,000
- Resolve critical errors
- Handle regulatory issues
- Process refunds > $50,000
- Make policy decisions

### Level 4: Executive
- Approve wires > $1,000,000
- Handle regulatory investigations
- Resolve major system issues
- Make strategic decisions

## Training Requirements

### New Employee Training
- Complete wire transfer basics course
- Shadow experienced team member for 2 weeks
- Pass wire processing certification
- Understand compliance requirements
- Learn error handling procedures

### Ongoing Training
- Quarterly compliance updates
- Annual security training
- Fraud detection workshops
- System updates and changes
- Best practices sharing sessions

## Appendix: Wire Transfer Codes

### Purpose Codes (International)
- SAL: Salary payment
- PENS: Pension payment
- DIVD: Dividend payment
- INTC: Interest payment
- GOVT: Government payment
- TRAD: Trade payment
- INVS: Investment payment
- SUPP: Supplier payment

### Status Codes
- PEND: Pending submission
- SUBM: Submitted to bank
- PROC: Processing
- COMP: Completed
- FAIL: Failed
- RTND: Returned
- CANC: Cancelled

### Error Codes
- INSUF_FUNDS: Insufficient funds
- INVALID_ACCT: Invalid account details
- COMPLIANCE: Compliance rejection
- NETWORK_ERR: Network error
- TIMEOUT: Processing timeout
- DUPLICATE: Duplicate transaction
