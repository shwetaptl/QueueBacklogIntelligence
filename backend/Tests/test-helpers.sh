#---------
#send_messages 10
#receive_messages 5
#check_queue
#---------

# ── CONFIGURATION ─────────────────────────────────────────────────────
RG="qbi-rg"
NS="qbi-sb-ns"
QUEUE="qbi-queue"

# ── SEND N MESSAGES ───────────────────────────────────────────────────
send_messages() {
    local count=$1
    local prefix=${2:-"test"}
    echo "Sending $count messages..."
    for i in $(seq 1 $count); do
        az servicebus message send \
            --resource-group $RG \
            --namespace-name $NS \
            --queue-name $QUEUE \
            --body "{\"orderId\": \"${prefix}-${i}\", \"item\": \"burger\"}" \
            2>/dev/null
    done
    echo "Done. Sent $count messages."
}

# ── RECEIVE N MESSAGES ────────────────────────────────────────────────
receive_messages() {
    local count=${1:-10}
    echo "Receiving up to $count messages..."
    az servicebus message receive \
        --resource-group $RG \
        --namespace-name $NS \
        --queue-name $QUEUE \
        --max-message-count $count \
        --receive-mode receiveanddelete \
        2>/dev/null
    echo "Done receiving."
}

# ── CHECK QUEUE COUNT ─────────────────────────────────────────────────
check_queue() {
    az servicebus queue show \
        --resource-group $RG \
        --namespace-name $NS \
        --name $QUEUE \
        --query "countDetails.activeMessageCount" \
        --output tsv
}

echo "Helper functions loaded."
echo "Available: send_messages, receive_messages, check_queue"