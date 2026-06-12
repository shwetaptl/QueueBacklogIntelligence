#!/bin/bash
# ═══════════════════════════════════════════════════════════════════════════
# QBIS Test Scenarios Script
# Run this WHILE func start is running in another terminal
#
# Usage:
#   ./test_scenarios.sh          → interactive menu
#   ./test_scenarios.sh purge    → just purge the queue
#   ./test_scenarios.sh check    → just check current data
# ═══════════════════════════════════════════════════════════════════════════

# ── Config ─────────────────────────────────────────────────────────────────
NAMESPACE="qbi-sb-ns"
QUEUE="qbi-queue"
RESOURCE_GROUP="qbi-rg"
STORAGE_ACCOUNT="queuebacklogsa"

# Read connection string from local.settings.json
STORAGE_CONN=$(python3 -c "
import json
with open('local.settings.json') as f:
    d = json.load(f)
print(d['Values'].get('StorageConnectionString',''))
" 2>/dev/null)

# ── Colors ──────────────────────────────────────────────────────────────────
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m'

# ── Helpers ─────────────────────────────────────────────────────────────────

queue_count() {
    az servicebus queue show \
      --resource-group "$RESOURCE_GROUP" \
      --namespace-name "$NAMESPACE" \
      --name "$QUEUE" \
      --query "countDetails.activeMessageCount" \
      --output tsv 2>/dev/null
}

purge_queue() {
    echo -e "${YELLOW}Purging queue...${NC}"
    az servicebus queue delete \
      --resource-group "$RESOURCE_GROUP" \
      --namespace-name "$NAMESPACE" \
      --name "$QUEUE" 2>/dev/null
    az servicebus queue create \
      --resource-group "$RESOURCE_GROUP" \
      --namespace-name "$NAMESPACE" \
      --name "$QUEUE" 2>/dev/null
    echo -e "${GREEN}Queue purged and recreated.${NC}"
    sleep 2
    echo -e "Current count: $(queue_count)"
}

send_messages() {
    local count=$1
    local label=$2
    echo -e "${CYAN}Sending $count messages ($label)...${NC}"
    echo -e "${YELLOW}→ Go to Azure Portal → $NAMESPACE → $QUEUE → Service Bus Explorer${NC}"
    echo -e "${YELLOW}→ Click Send → Repeat Send → Count: $count → Interval: 200ms → Send${NC}"
    echo ""
    read -p "Press ENTER when done sending $count messages... "
    local actual=$(queue_count)
    echo -e "${GREEN}Queue now has: $actual messages${NC}"
}

wait_and_check() {
    local seconds=$1
    local label=$2
    echo ""
    echo -e "${BLUE}Waiting ${seconds}s for QBIS to process ($label)...${NC}"
    local elapsed=0
    while [ $elapsed -lt $seconds ]; do
        sleep 10
        elapsed=$((elapsed + 10))
        local count=$(queue_count)
        echo -e "  ${elapsed}s elapsed — Queue: ${BOLD}$count messages${NC}"
    done
}

check_data() {
    local rows=${1:-10}
    echo ""
    echo -e "${BOLD}═══ LATEST $rows SNAPSHOTS ═══${NC}"
    az storage entity query \
      --account-name "$STORAGE_ACCOUNT" \
      --table-name QueueSnapshot \
      --filter "PartitionKey eq '$QUEUE'" \
      --num-results "$rows" \
      --connection-string "$STORAGE_CONN" \
      --output json 2>/dev/null | python3 -c "
import json, sys
rows = json.load(sys.stdin).get('items', [])
print('  {:<22} {:>8} {:>8} {:>8} {:>5}'.format('Time','Active','In/min','Out/min','DLQ'))
print('  ' + '-'*55)
for r in rows:
    active = r.get('ActiveCount', {})
    active = active.get('value', active) if isinstance(active, dict) else active
    dlq = r.get('DLQCount', {})
    dlq = dlq.get('value', dlq) if isinstance(dlq, dict) else dlq
    t = str(r.get('TimestampUtc','?'))[:19]
    inp = r.get('IncomingPerMin','?')
    out = r.get('OutgoingPerMin','?')
    print(f'  {t:<22} {str(active):>8} {str(inp):>8} {str(out):>8} {str(dlq):>5}')
"
    echo ""
    echo -e "${BOLD}═══ LATEST $rows STATUS ROWS ═══${NC}"
    az storage entity query \
      --account-name "$STORAGE_ACCOUNT" \
      --table-name QueueStatus \
      --filter "PartitionKey eq '$QUEUE'" \
      --num-results "$rows" \
      --connection-string "$STORAGE_CONN" \
      --output json 2>/dev/null | python3 -c "
import json, sys

SEV_COLOR = {'Critical': '\033[0;31m', 'Warning': '\033[1;33m', 'None': '\033[0;32m'}
SLA_COLOR = {'BREACHING': '\033[0;31m', 'OK': '\033[0;32m', 'UNKNOWN': '\033[1;33m'}
NC = '\033[0m'

rows = json.load(sys.stdin).get('items', [])
print(f\"  {'Time':<22} {'Active':>7} {'Trend':<13} {'SLA':<10} {'Severity':<10} {'Cause':<20} {'Wait':<8} {'Gap':>6} {'Send':>5}\")
print('  ' + '-'*105)
for r in rows:
    active = r.get('ActiveCount', {})
    active = active.get('value', active) if isinstance(active, dict) else active
    t      = str(r.get('TimestampUtc','?'))[:19]
    trend  = str(r.get('TrendLabel','?'))
    sla    = str(r.get('SlaStatus','?'))
    sev    = str(r.get('AlertSeverity','?'))
    cause  = str(r.get('RootCause','?'))
    wait   = r.get('WaitTimeMinutes','')
    gap    = r.get('GapPerMin','?')
    send   = str(r.get('NeedToSendAlert','?'))
    wait_s = f'{wait:.2f}m' if isinstance(wait, (int, float)) else '∞'
    sc = SEV_COLOR.get(sev, '')
    lc = SLA_COLOR.get(sla, '')
    print(f'  {t:<22} {str(active):>7} {trend:<13} {lc}{sla:<10}{NC} {sc}{sev:<10}{NC} {cause:<20} {wait_s:<8} {str(gap):>6} {send:>5}')
"
    echo ""
}

verify_scenario() {
    local scenario=$1
    local expected_trend=$2
    local expected_sla=$3
    local expected_severity=$4
    local expected_cause=$5

    echo ""
    echo -e "${BOLD}─── VERIFICATION: $scenario ───${NC}"
    echo -e "Expected: Trend=${CYAN}$expected_trend${NC} SLA=${CYAN}$expected_sla${NC} Severity=${CYAN}$expected_severity${NC} Cause=${CYAN}$expected_cause${NC}"

    cat > /tmp/qbis_verify.py << PYEOF2
import json, sys
rows = json.load(sys.stdin).get("items", [])
if rows:
    r = rows[0]
    active = r.get("ActiveCount", {})
    active = active.get("value", active) if isinstance(active, dict) else active
    print("{}|{}|{}|{}|{}".format(
        r.get("TrendLabel","?"),
        r.get("SlaStatus","?"),
        r.get("AlertSeverity","?"),
        r.get("RootCause","?"),
        active))
PYEOF2

    local result=$(az storage entity query \
      --account-name "$STORAGE_ACCOUNT" \
      --table-name QueueStatus \
      --filter "PartitionKey eq '$QUEUE'" \
      --num-results 1 \
      --connection-string "$STORAGE_CONN" \
      --output json 2>/dev/null | python3 /tmp/qbis_verify.py)
    local actual_trend=$(echo "$result" | cut -d'|' -f1)
    local actual_sla=$(echo "$result" | cut -d'|' -f2)
    local actual_sev=$(echo "$result" | cut -d'|' -f3)
    local actual_cause=$(echo "$result" | cut -d'|' -f4)
    local actual_active=$(echo "$result" | cut -d'|' -f5)

    echo -e "Actual:   Trend=${YELLOW}$actual_trend${NC} SLA=${YELLOW}$actual_sla${NC} Severity=${YELLOW}$actual_sev${NC} Cause=${YELLOW}$actual_cause${NC} (Active=$actual_active)"

    local pass=true
    [ "$expected_trend" != "ANY"     ] && [ "$actual_trend" != "$expected_trend" ]   && pass=false
    [ "$expected_sla" != "ANY"       ] && [ "$actual_sla" != "$expected_sla" ]         && pass=false
    [ "$expected_severity" != "ANY"  ] && [ "$actual_sev" != "$expected_severity" ]   && pass=false
    [ "$expected_cause" != "ANY"     ] && [ "$actual_cause" != "$expected_cause" ]     && pass=false

    if $pass; then
        echo -e "${GREEN}${BOLD}✅ PASS${NC}"
    else
        echo -e "${RED}${BOLD}❌ FAIL${NC}"
    fi
    echo ""
}


# ═══════════════════════════════════════════════════════════════════════════
# SCENARIOS
# ═══════════════════════════════════════════════════════════════════════════

scenario_1_empty_baseline() {
    echo ""
    echo -e "${BOLD}${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${BOLD}${BLUE} TC-01: EMPTY QUEUE BASELINE${NC}"
    echo -e "${BOLD}${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "What:    Queue is completely empty, no messages, no traffic"
    echo -e "Expect:  Idle/Stable, OK, None, Healthy"
    echo -e "Verifies: No false alarms on empty queue"
    echo ""
    purge_queue
    wait_and_check 150 "letting 2 analyzer cycles run"
    check_data 5

    cat > /tmp/qbis_check_tc01.py << PYEOF2
import json, sys
rows = json.load(sys.stdin).get("items", [])
# Find most recent row where Active=0
for r in rows:
    active = r.get("ActiveCount", {})
    active = active.get("value", active) if isinstance(active, dict) else active
    if int(active) == 0:
        sla = r.get("SlaStatus","")
        sev = r.get("AlertSeverity","")
        cause = r.get("RootCause","")
        trend = r.get("TrendLabel","")
        if sla == "OK" and cause in ["Healthy","Unknown"] and trend in ["Idle","Stable","Unknown"]:
            print("PASS:" + trend)
        else:
            print("FAIL:sla=" + sla + " sev=" + sev + " cause=" + cause + " trend=" + trend)
        break
else:
    print("FAIL:no empty queue row found")
PYEOF2

    echo ""
    echo -e "${BOLD}─── VERIFICATION: TC-01 Empty Baseline ───${NC}"
    echo -e "Expected: Empty queue shows OK, None, Healthy/Idle"
    RESULT=$(az storage entity query       --account-name "$STORAGE_ACCOUNT"       --table-name QueueStatus       --filter "PartitionKey eq '$QUEUE'"       --num-results 5       --connection-string "$STORAGE_CONN"       --output json 2>/dev/null | python3 /tmp/qbis_check_tc01.py)

    if [[ "$RESULT" == PASS* ]]; then
        TREND=$(echo "$RESULT" | cut -d: -f2)
        echo -e "${GREEN}${BOLD}✅ PASS — Empty queue correctly shows $TREND/OK/None/Healthy${NC}"
    else
        echo -e "${RED}${BOLD}❌ FAIL — $RESULT${NC}"
    fi
    echo ""
    read -p "Press ENTER to continue to next scenario..."
}

scenario_2_consumer_stopped() {
    echo ""
    echo -e "${BOLD}${RED}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${BOLD}${RED} TC-02: CONSUMER STOPPED${NC}"
    echo -e "${BOLD}${RED}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "What:    Send 20 messages, no consumer running"
    echo -e "Expect:  Growing→Stable, BREACHING, Critical, ConsumerStopped"
    echo -e "Verifies: Consumer crash detected within 2 minutes"
    echo ""
    purge_queue
    send_messages 20 "TC-02 consumer stopped test"
    echo -e "${YELLOW}DO NOT consume any messages. Let them sit.${NC}"
    wait_and_check 180 "waiting for ConsumerStopped detection"
    check_data 8

    # Verify by checking if ConsumerStopped appeared in recent rows
    # (not just the latest row which may have changed)
    echo ""
    echo -e "\033[1m─── VERIFICATION: TC-02 Consumer Stopped ───\033[0m"
    echo -e "Expected: Any recent row has SLA=BREACHING Severity=Critical Cause=ConsumerStopped"

    cat > /tmp/qbis_check_tc02.py << PYEOF2
import json, sys
rows = json.load(sys.stdin).get("items", [])
for r in rows:
    if (r.get("RootCause") == "ConsumerStopped" and
        r.get("SlaStatus") == "BREACHING" and
        r.get("AlertSeverity") == "Critical"):
        print("FOUND")
        break
PYEOF2
    FOUND=$(az storage entity query       --account-name "$STORAGE_ACCOUNT"       --table-name QueueStatus       --filter "PartitionKey eq '$QUEUE'"       --num-results 10       --connection-string "$STORAGE_CONN"       --output json 2>/dev/null | python3 /tmp/qbis_check_tc02.py)
    if [ "$FOUND" == "FOUND" ]; then
        echo -e "\033[0;32m\033[1m✅ PASS — ConsumerStopped + Critical + BREACHING found in history\033[0m"
    else
        echo -e "\033[0;31m\033[1m❌ FAIL — ConsumerStopped not found in recent history\033[0m"
    fi
    echo ""
    read -p "Press ENTER to continue..."
}

scenario_3_growing_backlog() {
    echo ""
    echo -e "${BOLD}${RED}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${BOLD}${RED} TC-03: GROWING BACKLOG (Multiple Rounds)${NC}"
    echo -e "${BOLD}${RED}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "What:    Send 10 messages every minute for 4 rounds, no consumer"
    echo -e "Expect:  Growing/GrowingFast, BREACHING, Critical"
    echo -e "Verifies: Sustained growth detected, severity escalates"
    echo ""
    purge_queue

    for round in 1 2 3 4; do
        echo -e "${CYAN}Round $round of 4${NC}"
        send_messages 10 "round $round"
        if [ $round -lt 4 ]; then
            echo "Waiting 60s before next round..."
            sleep 60
        fi
    done

    wait_and_check 120 "letting analyzer catch up"
    check_data 10

    # TC-03: verify Growing appeared in recent history
    cat > /tmp/qbis_check_tc03.py << PYEOF2
import json, sys
rows = json.load(sys.stdin).get("items", [])
found_growing = False
found_critical = False
for r in rows:
    trend = r.get("TrendLabel","")
    sev   = r.get("AlertSeverity","")
    sla   = r.get("SlaStatus","")
    if trend in ["Growing","GrowingFast"] and sla == "BREACHING":
        found_growing = True
    if sev == "Critical" and sla == "BREACHING":
        found_critical = True
if found_growing and found_critical:
    print("PASS")
else:
    print("FAIL:growing=" + str(found_growing) + " critical=" + str(found_critical))
PYEOF2

    echo ""
    echo -e "${BOLD}─── VERIFICATION: TC-03 Growing Backlog ───${NC}"
    echo -e "Expected: Growing+BREACHING in history AND Critical+BREACHING in history"
    RESULT=$(az storage entity query       --account-name "$STORAGE_ACCOUNT"       --table-name QueueStatus       --filter "PartitionKey eq '$QUEUE'"       --num-results 10       --connection-string "$STORAGE_CONN"       --output json 2>/dev/null | python3 /tmp/qbis_check_tc03.py)

    if [ "$RESULT" == "PASS" ]; then
        echo -e "${GREEN}${BOLD}✅ PASS — Growing trend and Critical severity confirmed in history${NC}"
    else
        echo -e "${RED}${BOLD}❌ FAIL — $RESULT${NC}"
    fi
    echo ""
    read -p "Press ENTER to continue..."
}

scenario_4_stable_balanced() {
    echo ""
    echo -e "${BOLD}${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${BOLD}${GREEN} TC-04: STABLE BALANCED QUEUE${NC}"
    echo -e "${BOLD}${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "What:    Send 10 messages then immediately consume 10, repeat 3x"
    echo -e "Expect:  Stable, OK, None"
    echo -e "Verifies: No false alarms when queue is balanced"
    echo ""
    purge_queue

    for round in 1 2 3; do
        echo -e "${CYAN}Round $round of 3${NC}"
        send_messages 10 "balanced round $round"
        echo ""
        echo -e "${YELLOW}Now consume ALL messages:${NC}"
        echo -e "${YELLOW}→ Portal → Service Bus Explorer → Receive → ReceiveAndDelete → Receive 10${NC}"
        read -p "Press ENTER when all consumed..."
        local cnt=$(queue_count)
        echo -e "Queue count: $cnt"
        sleep 30
    done

    wait_and_check 120 "letting analyzer see stable state"
    check_data 8
    verify_scenario "TC-04 Stable Balanced" "ANY" "OK" "None" "ANY"
    read -p "Press ENTER to continue..."
}

scenario_5_recovery() {
    echo ""
    echo -e "${BOLD}${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${BOLD}${CYAN} TC-05: AUTO RECOVERY AFTER FIX${NC}"
    echo -e "${BOLD}${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "What:    Create ConsumerStopped incident, then consume all messages"
    echo -e "Expect:  ConsumerStopped → Critical, then Recovering → OK → None"
    echo -e "Verifies: Alert auto-resolves, recovery detected correctly"
    echo ""
    purge_queue
    send_messages 20 "TC-05 create incident"
    echo -e "${YELLOW}Wait 3 minutes to establish ConsumerStopped...${NC}"
    wait_and_check 180 "establishing incident"

    echo ""
    echo -e "${YELLOW}Now consume ALL messages to trigger recovery:${NC}"
    echo -e "${YELLOW}→ Portal → Service Bus Explorer → Receive → ReceiveAndDelete → Receive 25${NC}"
    read -p "Press ENTER when all consumed..."

    wait_and_check 180 "waiting for recovery detection"
    check_data 10
    verify_scenario "TC-05 Recovery" "ANY" "OK" "ANY" "Recovering"
    read -p "Press ENTER to continue..."
}

scenario_6_slow_drain_breach() {
    echo ""
    echo -e "${BOLD}${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${BOLD}${YELLOW} TC-06: SLOW DRAIN WHILE BREACHING SLA${NC}"
    echo -e "${BOLD}${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "What:    Send 40 messages, consume 3 at a time slowly"
    echo -e "Expect:  Draining, BREACHING, Critical (still violating SLA while draining)"
    echo -e "Verifies: System correctly shows BREACHING even when queue is technically improving"
    echo ""
    purge_queue
    send_messages 40 "TC-06 large backlog"
    echo -e "${YELLOW}Wait 2 minutes to establish incident...${NC}"
    wait_and_check 120 "establishing incident"

    echo ""
    echo -e "${YELLOW}Now consume 8 messages (first drain batch):${NC}"
    echo -e "${YELLOW}→ Portal → Service Bus Explorer → Receive → ReceiveAndDelete → Receive 8${NC}"
    read -p "Press ENTER when consumed 8..."
    echo "Waiting 70s for Collector + Analyzer cycle..."
    sleep 70

    echo -e "${YELLOW}Consume 8 more messages (second drain batch):${NC}"
    echo -e "${YELLOW}→ Portal → Service Bus Explorer → Receive → ReceiveAndDelete → Receive 8${NC}"
    read -p "Press ENTER when consumed 8..."

    wait_and_check 90 "checking slow drain behavior"
    check_data 8

    # TC-06 verifies BREACHING + Critical persists even while draining
    # Trend can be Draining or Stable depending on smoothed delta
    echo ""
    echo -e "${BOLD}─── VERIFICATION: TC-06 Slow Drain ───${NC}"
    echo -e "Expected: SLA=BREACHING AND Severity=Critical (queue improving but still breaching)"

    # Write python script to temp file to avoid quoting issues
    cat > /tmp/qbis_check_tc06.py << PYEOF2
import json, sys
rows = json.load(sys.stdin).get("items", [])
for r in rows:
    sla   = r.get("SlaStatus","")
    sev   = r.get("AlertSeverity","")
    trend = r.get("TrendLabel","")
    if sla == "BREACHING" and sev == "Critical" and trend in ["Draining","DrainingFast","Stable"]:
        print("FOUND:" + trend)
        break
PYEOF2

    FOUND=$(az storage entity query       --account-name "$STORAGE_ACCOUNT"       --table-name QueueStatus       --filter "PartitionKey eq '$QUEUE'"       --num-results 8       --connection-string "$STORAGE_CONN"       --output json 2>/dev/null | python3 /tmp/qbis_check_tc06.py)

    if [[ "$FOUND" == FOUND* ]]; then
        TREND=$(echo "$FOUND" | cut -d: -f2)
        echo -e "${GREEN}${BOLD}✅ PASS — BREACHING+Critical while $TREND (queue improving but SLA still violated)${NC}"
    else
        echo -e "${RED}${BOLD}❌ FAIL — Expected BREACHING+Critical during drain${NC}"
    fi
    echo ""
    read -p "Press ENTER to continue..."
}

scenario_7_dlq_growth() {
    echo ""
    echo -e "${BOLD}${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${BOLD}${YELLOW} TC-07: DLQ GROWTH${NC}"
    echo -e "${BOLD}${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "What:    Send messages with short TTL — they expire and move to Dead Letter Queue"
    echo -e "Expect:  DLQGrowth root cause detected"
    echo -e "Verifies: DLQ growth detected independently from active queue"
    echo ""

    # Must start with clean queue so DLQ count starts at 0
    purge_queue
    echo -e "${YELLOW}Also clear DLQ: Portal → qbi-queue → Dead-letter → Select All → Delete${NC}"
    read -p "Press ENTER when DLQ is also cleared..."

    echo ""
    echo -e "${BOLD}Steps to send messages with TTL:${NC}"
    echo -e "${YELLOW}1. Portal → qbi-sb-ns → qbi-queue → Service Bus Explorer${NC}"
    echo -e "${YELLOW}2. Click Send tab${NC}"
    echo -e "${YELLOW}3. Find 'Time To Live' field → set to: PT1M (1 minute)${NC}"
    echo -e "${YELLOW}4. Click 'Repeat Send' → Count: 15 → Interval: 200ms → Send${NC}"
    echo -e "${YELLOW}5. The messages will expire to DLQ after 1 minute${NC}"
    echo ""
    read -p "Press ENTER when done sending 15 messages with TTL=PT1M..."
    local cnt=$(queue_count)
    echo -e "Active queue: $cnt messages"

    echo ""
    echo -e "${CYAN}Waiting 90s for messages to expire to DLQ...${NC}"
    for i in $(seq 1 9); do
        sleep 10
        local dlq=$(az servicebus queue show           --resource-group "$RESOURCE_GROUP"           --namespace-name "$NAMESPACE"           --name "$QUEUE"           --query "countDetails.deadLetterMessageCount"           --output tsv 2>/dev/null)
        local active=$(queue_count)
        echo -e "  ${i}0s — Active=$active  DLQ=${dlq}"
    done

    wait_and_check 90 "waiting for QBIS to detect DLQ growth"
    check_data 8

    # Verify DLQGrowth in recent status history
    cat > /tmp/qbis_check_tc07.py << PYEOF2
import json, sys
rows = json.load(sys.stdin).get("items", [])
for r in rows:
    cause = r.get("RootCause","")
    dlq_raw = r.get("DLQCount",{})
    dlq = dlq_raw.get("value", dlq_raw) if isinstance(dlq_raw, dict) else dlq_raw
    if cause == "DLQGrowth":
        print("FOUND:DLQGrowth detected")
        break
    if int(dlq or 0) > 5:
        print("DLQ_PRESENT:count=" + str(dlq) + " but cause=" + cause)
        break
else:
    print("NOT_FOUND:DLQ may not have grown yet")
PYEOF2

    echo ""
    echo -e "${BOLD}─── VERIFICATION: TC-07 DLQ Growth ───${NC}"
    RESULT=$(az storage entity query       --account-name "$STORAGE_ACCOUNT"       --table-name QueueStatus       --filter "PartitionKey eq '$QUEUE'"       --num-results 10       --connection-string "$STORAGE_CONN"       --output json 2>/dev/null | python3 /tmp/qbis_check_tc07.py)

    if [[ "$RESULT" == FOUND* ]]; then
        echo -e "${GREEN}${BOLD}✅ PASS — $RESULT${NC}"
    elif [[ "$RESULT" == DLQ_PRESENT* ]]; then
        echo -e "${YELLOW}${BOLD}⚠ PARTIAL — DLQ has messages but RootCause not yet DLQGrowth${NC}"
        echo -e "${YELLOW}Wait 2 more minutes and check again with option [c]${NC}"
    else
        echo -e "${RED}${BOLD}❌ FAIL — $RESULT${NC}"
        echo -e "${RED}Check that DeadLetteringOnMessageExpiration is enabled on the queue${NC}"
        echo -e "${RED}Portal → qbi-queue → Properties → Dead lettering on message expiration = ON${NC}"
    fi
    echo ""
    read -p "Press ENTER to continue..."
}

scenario_8_idle_stale() {
    echo ""
    echo -e "${BOLD}${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${BOLD}${GREEN} TC-08: IDLE QUEUE WITH STALE MESSAGES${NC}"
    echo -e "${BOLD}${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "What:    Leave 2 messages in queue with no traffic for 5+ minutes"
    echo -e "Expect:  Idle, OK, None — NOT ConsumerStopped"
    echo -e "Verifies: Stale messages do not trigger false Critical alert"
    echo ""
    purge_queue
    send_messages 2 "TC-08 stale messages"
    echo -e "${YELLOW}DO NOT consume. Wait 5 minutes for system to recognize as idle.${NC}"
    wait_and_check 300 "establishing idle state"
    check_data 8
    verify_scenario "TC-08 Idle Stale" "Idle" "OK" "None" "Healthy"
    read -p "Press ENTER to continue..."
}

scenario_9_burst_arrival() {
    echo ""
    echo -e "${BOLD}${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${BOLD}${YELLOW} TC-09: BURST ARRIVAL ON EMPTY QUEUE${NC}"
    echo -e "${BOLD}${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "What:    Queue empty, suddenly 25 messages arrive"
    echo -e "         Azure Monitor shows Out=0 due to delay"
    echo -e "Expect:  Growing (NOT ConsumerStopped), UNKNOWN/BREACHING"
    echo -e "Verifies: Burst arrival not mistaken for consumer crash"
    echo ""
    purge_queue
    wait_and_check 90 "confirming empty state"
    send_messages 25 "TC-09 burst arrival"
    wait_and_check 90 "checking initial reaction"
    check_data 6

    # This scenario ConsumerStopped is acceptable in minute 1
    # but should NOT persist if consumer starts consuming
    echo -e "${CYAN}Now consume all messages to see transition:${NC}"
    echo -e "${YELLOW}→ Portal → Service Bus Explorer → Receive → ReceiveAndDelete → Receive 30${NC}"
    read -p "Press ENTER when consumed..."
    wait_and_check 120 "checking after consumption"
    check_data 8
    verify_scenario "TC-09 Burst Recovery" "ANY" "OK" "None" "ANY"
    read -p "Press ENTER to continue..."
}

scenario_10_full_lifecycle() {
    echo ""
    echo -e "${BOLD}${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${BOLD}${BLUE} TC-10: FULL LIFECYCLE (Combined Scenario)${NC}"
    echo -e "${BOLD}${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "What:    Complete incident lifecycle:"
    echo -e "         Phase 1: Empty queue (OK)"
    echo -e "         Phase 2: Messages arrive, consumer stopped (Critical)"
    echo -e "         Phase 3: Consumer restarted, slow drain (BREACHING+Draining)"
    echo -e "         Phase 4: Queue cleared (Recovery)"
    echo -e "         Phase 5: Back to idle (OK)"
    echo ""

    echo -e "${BOLD}PHASE 1: Establish empty baseline${NC}"
    purge_queue
    wait_and_check 90 "empty baseline"
    check_data 3

    echo -e "${BOLD}PHASE 2: Consumer stopped incident${NC}"
    send_messages 30 "phase 2 - incident"
    wait_and_check 180 "ConsumerStopped detection"
    check_data 5

    echo -e "${BOLD}PHASE 3: Slow drain (simulate consumer restarted but slow)${NC}"
    echo -e "${YELLOW}Consume 5 messages (simulating slow consumer):${NC}"
    echo -e "${YELLOW}→ Portal → Service Bus Explorer → Receive → ReceiveAndDelete → Receive 5${NC}"
    read -p "Press ENTER when consumed 5..."
    wait_and_check 120 "slow drain phase"
    check_data 5

    echo -e "${BOLD}PHASE 4: Full recovery${NC}"
    echo -e "${YELLOW}Consume ALL remaining messages:${NC}"
    echo -e "${YELLOW}→ Portal → Service Bus Explorer → Receive → ReceiveAndDelete → Receive 30${NC}"
    read -p "Press ENTER when all consumed..."
    wait_and_check 180 "recovery detection"
    check_data 10

    echo ""
    echo -e "${BOLD}Full lifecycle complete. Check data above for complete story.${NC}"
    echo -e "Expected progression:"
    echo -e "  Healthy → ConsumerStopped/Critical → Recovering/Draining → OK/None"
    read -p "Press ENTER to return to menu..."
}


# ═══════════════════════════════════════════════════════════════════════════
# MAIN MENU
# ═══════════════════════════════════════════════════════════════════════════

if [ "$1" == "purge" ]; then
    purge_queue
    exit 0
fi

if [ "$1" == "check" ]; then
    check_data 15
    exit 0
fi

while true; do
    clear
    echo -e "${BOLD}${BLUE}"
    echo "╔═══════════════════════════════════════════════════════════╗"
    echo "║         QBIS TEST SCENARIOS — Interactive Menu            ║"
    echo "╚═══════════════════════════════════════════════════════════╝"
    echo -e "${NC}"
    echo -e "  ${GREEN}[1]${NC}  TC-01: Empty Queue Baseline         (no messages, no alerts)"
    echo -e "  ${RED}[2]${NC}  TC-02: Consumer Stopped              (Critical within 2min)"
    echo -e "  ${RED}[3]${NC}  TC-03: Growing Backlog               (escalating severity)"
    echo -e "  ${GREEN}[4]${NC}  TC-04: Stable Balanced Queue         (no false alarms)"
    echo -e "  ${CYAN}[5]${NC}  TC-05: Auto Recovery After Fix       (alert clears itself)"
    echo -e "  ${YELLOW}[6]${NC}  TC-06: Slow Drain + SLA Breach       (Draining but Critical)"
    echo -e "  ${YELLOW}[7]${NC}  TC-07: DLQ Growth                    (DLQ root cause)"
    echo -e "  ${GREEN}[8]${NC}  TC-08: Idle Stale Messages           (no false Critical)"
    echo -e "  ${YELLOW}[9]${NC}  TC-09: Burst Arrival on Empty Queue  (not ConsumerStopped)"
    echo -e "  ${BLUE}[10]${NC} TC-10: Full Lifecycle                (complete incident story)"
    echo ""
    echo -e "  ${CYAN}[c]${NC}  Check current data (last 15 rows)"
    echo -e "  ${CYAN}[p]${NC}  Purge queue"
    echo -e "  ${CYAN}[q]${NC}  Quit"
    echo ""
    echo -e "  ${YELLOW}⚠  Make sure func start is running in another terminal${NC}"
    echo ""
    read -p "Select scenario: " choice

    case $choice in
        1)  scenario_1_empty_baseline ;;
        2)  scenario_2_consumer_stopped ;;
        3)  scenario_3_growing_backlog ;;
        4)  scenario_4_stable_balanced ;;
        5)  scenario_5_recovery ;;
        6)  scenario_6_slow_drain_breach ;;
        7)  scenario_7_dlq_growth ;;
        8)  scenario_8_idle_stale ;;
        9)  scenario_9_burst_arrival ;;
        10) scenario_10_full_lifecycle ;;
        c|C) check_data 15 ; read -p "Press ENTER..." ;;
        p|P) purge_queue ; read -p "Press ENTER..." ;;
        q|Q) echo "Bye!" ; exit 0 ;;
        *)  echo "Invalid choice" ; sleep 1 ;;
    esac
done