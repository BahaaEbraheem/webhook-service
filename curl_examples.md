# Webhook Service API - cURL Examples

This file contains comprehensive cURL examples for testing all endpoints of the webhook service technical task implementation.

## Base Configuration

```bash
# Set base URL (adjust for your environment)
BASE_URL="http://localhost:5000"  # Docker
# BASE_URL="http://localhost:8080"  # Local development

# Test tenant ID
TENANT_ID="tenant-demo"
```

## 1. Health & Monitoring Endpoints

### Health Check
```bash
curl -X GET "${BASE_URL}/health" \
  -H "Accept: application/json" | jq
```

### Prometheus Metrics
```bash
curl -X GET "${BASE_URL}/metrics" \
  -H "Accept: text/plain"
```

## 2. Subscriber Management

### Create Subscriber
```bash
# Create a new webhook subscriber
SUBSCRIBER_RESPONSE=$(curl -X POST "${BASE_URL}/api/subscribers" \
  -H "Content-Type: application/json" \
  -d '{
    "tenantId": "'${TENANT_ID}'",
    "callbackUrl": "https://webhook.site/unique-id-here",
    "eventTypes": ["user.created", "order.completed", "payment.processed"]
  }' | jq)

echo "$SUBSCRIBER_RESPONSE"

# Extract subscriber ID for subsequent requests
SUBSCRIBER_ID=$(echo "$SUBSCRIBER_RESPONSE" | jq -r '.id')
echo "Subscriber ID: $SUBSCRIBER_ID"
```

### Rotate Subscriber Secret
```bash
# Rotate the secret key for security
curl -X POST "${BASE_URL}/api/subscribers/${SUBSCRIBER_ID}/rotate-secret" \
  -H "Accept: application/json" | jq
```

### Get Subscriber Status
```bash
# Check subscriber configuration and status
curl -X GET "${BASE_URL}/api/subscribers/${SUBSCRIBER_ID}/status" \
  -H "Accept: application/json" | jq
```

## 3. Event Ingestion

### Send User Created Event (with Idempotency)
```bash
# Send event with idempotency key to prevent duplicates
IDEMPOTENCY_KEY="user-created-$(date +%s)-$(shuf -i 1000-9999 -n 1)"

EVENT_RESPONSE=$(curl -X POST "${BASE_URL}/api/events" \
  -H "Content-Type: application/json" \
  -H "X-Idempotency-Key: ${IDEMPOTENCY_KEY}" \
  -d '{
    "tenantId": "'${TENANT_ID}'",
    "eventType": "user.created",
    "payload": {
      "userId": "user-'$(shuf -i 1000-9999 -n 1)'",
      "email": "user'$(shuf -i 1000-9999 -n 1)'@example.com",
      "name": "Test User",
      "createdAt": "'$(date -Iseconds)'"
    }
  }' | jq)

echo "$EVENT_RESPONSE"

# Extract event ID for delivery queries
EVENT_ID=$(echo "$EVENT_RESPONSE" | jq -r '.eventId')
echo "Event ID: $EVENT_ID"
```

### Send Order Completed Event
```bash
# Test different event type for filtering
curl -X POST "${BASE_URL}/api/events" \
  -H "Content-Type: application/json" \
  -H "X-Idempotency-Key: order-$(date +%s)-$(shuf -i 1000-9999 -n 1)" \
  -d '{
    "tenantId": "'${TENANT_ID}'",
    "eventType": "order.completed",
    "payload": {
      "orderId": "order-'$(shuf -i 1000-9999 -n 1)'",
      "customerId": "customer-'$(shuf -i 1000-9999 -n 1)'",
      "amount": '$(shuf -i 100-1000 -n 1)',
      "currency": "USD",
      "completedAt": "'$(date -Iseconds)'"
    }
  }' | jq
```

### Test Idempotency (Duplicate Event)
```bash
# Send the same event again - should return 409 Conflict
curl -X POST "${BASE_URL}/api/events" \
  -H "Content-Type: application/json" \
  -H "X-Idempotency-Key: ${IDEMPOTENCY_KEY}" \
  -d '{
    "tenantId": "'${TENANT_ID}'",
    "eventType": "user.created",
    "payload": {
      "userId": "duplicate-test",
      "email": "duplicate@example.com"
    }
  }' -w "\nHTTP Status: %{http_code}\n" | jq
```

## 4. Delivery Logs & Monitoring

### Get All Deliveries (Paginated)
```bash
# Retrieve delivery logs with pagination
curl -X GET "${BASE_URL}/api/deliveries?page=1&pageSize=10" \
  -H "Accept: application/json" | jq
```

### Filter Deliveries by Event ID
```bash
# Get deliveries for specific event
curl -X GET "${BASE_URL}/api/deliveries?eventId=${EVENT_ID}&page=1&pageSize=5" \
  -H "Accept: application/json" | jq
```

### Filter Deliveries by Subscriber ID
```bash
# Get deliveries for specific subscriber
curl -X GET "${BASE_URL}/api/deliveries?subscriberId=${SUBSCRIBER_ID}&page=1&pageSize=5" \
  -H "Accept: application/json" | jq
```

### Filter Failed Deliveries
```bash
# Get only failed deliveries (status = 2)
curl -X GET "${BASE_URL}/api/deliveries?status=2&page=1&pageSize=10" \
  -H "Accept: application/json" | jq
```

### Filter Deliveries by Date Range
```bash
# Get deliveries from last hour
FROM_DATE=$(date -d '1 hour ago' -Iseconds)
TO_DATE=$(date -Iseconds)

curl -X GET "${BASE_URL}/api/deliveries?from=${FROM_DATE}&to=${TO_DATE}&page=1&pageSize=10" \
  -H "Accept: application/json" | jq
```

## 5. Testing Scenarios

### Complete Happy Path Test
```bash
#!/bin/bash
echo "=== Complete Happy Path Test ==="

# 1. Create subscriber
echo "1. Creating subscriber..."
SUBSCRIBER=$(curl -s -X POST "${BASE_URL}/api/subscribers" \
  -H "Content-Type: application/json" \
  -d '{
    "tenantId": "'${TENANT_ID}'",
    "callbackUrl": "https://webhook.site/test-endpoint",
    "eventTypes": ["user.created"]
  }')

SUBSCRIBER_ID=$(echo "$SUBSCRIBER" | jq -r '.id')
echo "   Subscriber created: $SUBSCRIBER_ID"

# 2. Send event
echo "2. Sending event..."
EVENT=$(curl -s -X POST "${BASE_URL}/api/events" \
  -H "Content-Type: application/json" \
  -H "X-Idempotency-Key: test-$(date +%s)" \
  -d '{
    "tenantId": "'${TENANT_ID}'",
    "eventType": "user.created",
    "payload": {"userId": "test-user", "email": "test@example.com"}
  }')

EVENT_ID=$(echo "$EVENT" | jq -r '.eventId')
echo "   Event created: $EVENT_ID"

# 3. Wait for delivery
echo "3. Waiting for webhook delivery..."
sleep 3

# 4. Check delivery status
echo "4. Checking delivery status..."
curl -s -X GET "${BASE_URL}/api/deliveries?eventId=${EVENT_ID}" \
  -H "Accept: application/json" | jq '.deliveries[0] | {status, httpStatusCode, durationMs, errorMessage}'

echo "=== Test Complete ==="
```

## 6. Error Testing

### Invalid Subscriber Data
```bash
# Test validation errors
curl -X POST "${BASE_URL}/api/subscribers" \
  -H "Content-Type: application/json" \
  -d '{
    "tenantId": "",
    "callbackUrl": "invalid-url",
    "eventTypes": []
  }' -w "\nHTTP Status: %{http_code}\n" | jq
```

### Invalid Event Data
```bash
# Test event validation
curl -X POST "${BASE_URL}/api/events" \
  -H "Content-Type: application/json" \
  -d '{
    "tenantId": "",
    "eventType": "",
    "payload": null
  }' -w "\nHTTP Status: %{http_code}\n" | jq
```

### Non-existent Subscriber
```bash
# Test 404 error
curl -X GET "${BASE_URL}/api/subscribers/00000000-0000-0000-0000-000000000000/status" \
  -H "Accept: application/json" -w "\nHTTP Status: %{http_code}\n" | jq
```

## 7. Performance Testing

### Bulk Event Creation
```bash
#!/bin/bash
echo "=== Bulk Event Test ==="

for i in {1..10}; do
  curl -s -X POST "${BASE_URL}/api/events" \
    -H "Content-Type: application/json" \
    -H "X-Idempotency-Key: bulk-test-${i}-$(date +%s)" \
    -d '{
      "tenantId": "'${TENANT_ID}'",
      "eventType": "user.created",
      "payload": {"userId": "bulk-user-'${i}'", "email": "bulk'${i}'@example.com"}
    }' > /dev/null &
done

wait
echo "Bulk events sent. Check deliveries:"
curl -s -X GET "${BASE_URL}/api/deliveries?page=1&pageSize=20" | jq '.totalCount'
```

## Status Codes Reference

- **200 OK**: Successful GET requests
- **201 Created**: Successful POST requests (subscriber/event creation)
- **400 Bad Request**: Validation errors
- **404 Not Found**: Resource not found
- **409 Conflict**: Duplicate idempotency key
- **500 Internal Server Error**: Server errors

## Delivery Status Values

- **0**: Pending
- **1**: Success  
- **2**: Failed
- **3**: Retrying
- **4**: DLQ (Dead Letter Queue)
