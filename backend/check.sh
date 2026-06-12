#!/bin/bash
CONN=$(grep StorageConnectionString local.settings.json | cut -d'"' -f4)

echo "=== LATEST SNAPSHOTS ==="
az storage entity query \
  --account-name queuebacklogsa \
  --table-name QueueSnapshot \
  --filter "PartitionKey eq 'qbi-queue'" \
  --num-results 125 \
  --connection-string "$CONN" \
  --output json | python3 -c "
import json,sys
rows = json.load(sys.stdin).get('items', [])
for r in rows:
    print(f\"Time={r.get('TimestampUtc','?')[:19]}  Active={r.get('ActiveCount','?')}  In={r.get('IncomingPerMin','?')}  Out={r.get('OutgoingPerMin','?')}  DLQ={r.get('DLQCount','?')}\")"

echo ""
echo "=== LATEST STATUS ==="
az storage entity query \
  --account-name queuebacklogsa \
  --table-name QueueStatus \
  --filter "PartitionKey eq 'qbi-queue'" \
  --num-results 125 \
  --connection-string "$CONN" \
  --output json | python3 -c "
import json,sys
rows = json.load(sys.stdin).get('items', [])
for r in rows:
    print(f\"Time={r.get('TimestampUtc','?')[:19]}  Active={r.get('ActiveCount','?')}  Trend={r.get('TrendLabel','?')}  SLA={r.get('SlaStatus','?')}  Severity={r.get('AlertSeverity','?')}  Cause={r.get('RootCause','?')}  Wait={r.get('WaitTimeMinutes','?')}  Gap={r.get('GapPerMin','?')}  Send={r.get('NeedToSendAlert','?')}\")"
