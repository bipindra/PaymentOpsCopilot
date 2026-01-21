# Authorization Rate Monitoring

## Overview
Authorization rates directly impact revenue. Monitor and alert on rate drops immediately.

## Key Metrics
- Authorization success rate (target: >95%)
- Decline rate by reason code
- Time-to-decline latency
- Network timeout rate

## Alert Thresholds
- Success rate drops below 90%: P0 alert
- Success rate drops below 95%: P1 alert
- Decline rate increases 20%: P1 alert

## Troubleshooting Checklist
When auth rate drops, check in this order:

1. **Payment Processor Status**
   - Check processor dashboard for outages
   - Verify API connectivity
   - Test with sample transaction

2. **Card Data Quality**
   - Verify card numbers are valid format
   - Check CVV presence
   - Validate expiration dates

3. **Fraud Rules**
   - Review recent fraud rule changes
   - Check if rules are too aggressive
   - Verify velocity limits

4. **Network Issues**
   - Check latency to processor
   - Verify DNS resolution
   - Test connectivity from multiple regions

5. **Configuration Errors**
   - Verify merchant ID is correct
   - Check API credentials haven't expired
   - Validate currency codes

## Recovery Steps
1. Identify root cause using checklist above
2. If processor issue: Enable failover to backup processor
3. If fraud rules: Temporarily relax rules, then tune
4. If network: Check CDN/load balancer health
5. Document incident in post-mortem
