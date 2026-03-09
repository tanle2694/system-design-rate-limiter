#!/usr/bin/env bash
set -uo pipefail

BASE_URL="${API_URL:-http://localhost:8080}"
PASS=0
FAIL=0
HEADER_FILE=$(mktemp)
BODY_FILE=$(mktemp)
trap 'rm -f "$HEADER_FILE" "$BODY_FILE"' EXIT

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

print_header() {
    echo ""
    echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${CYAN}  $1${NC}"
    echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
}

assert_status() {
    local description="$1" expected="$2" actual="$3"
    if [[ "$actual" == "$expected" ]]; then
        echo -e "  ${GREEN}PASS${NC} $description (status=$actual)"
        PASS=$((PASS + 1))
    else
        echo -e "  ${RED}FAIL${NC} $description (expected=$expected, got=$actual)"
        FAIL=$((FAIL + 1))
    fi
}

assert_header_exists() {
    local description="$1" header_value="$2"
    if [[ -n "$header_value" ]]; then
        echo -e "  ${GREEN}PASS${NC} $description (value=$header_value)"
        PASS=$((PASS + 1))
    else
        echo -e "  ${RED}FAIL${NC} $description (header missing)"
        FAIL=$((FAIL + 1))
    fi
}

# Portable request helper: do_request URL [EXTRA_CURL_ARGS...]
# Sets: STATUS, HEADERS (file), BODY (file)
do_request() {
    local url="$1"; shift
    STATUS=$(curl -s -o "$BODY_FILE" -D "$HEADER_FILE" -w "%{http_code}" "$url" "$@")
}

get_header() {
    grep -i "^$1:" "$HEADER_FILE" | head -1 | sed -E 's/^[^:]+: *//' | tr -d '\r\n'
}

# ─────────────────────────────────────────────────────────────
# Test 1: Basic endpoint returns 200 with rate limit headers
# ─────────────────────────────────────────────────────────────
print_header "Test 1: GET /hello — basic response + rate limit headers"

do_request "$BASE_URL/hello" -H "X-Forwarded-For: 10.0.0.1"
assert_status "GET /hello returns 200" "200" "$STATUS"
assert_header_exists "X-Ratelimit-Limit header present" "$(get_header x-ratelimit-limit)"
assert_header_exists "X-Ratelimit-Remaining header present" "$(get_header x-ratelimit-remaining)"

# ─────────────────────────────────────────────────────────────
# Test 2: Rate limiting triggers 429 on /hello (10 req/min)
# ─────────────────────────────────────────────────────────────
print_header "Test 2: GET /hello — rate limit exhaustion (10 req/min, token bucket)"

got_429=false
retry_after=""
for i in $(seq 1 15); do
    do_request "$BASE_URL/hello" -H "X-Forwarded-For: 10.0.0.2"
    if [[ "$STATUS" == "429" ]]; then
        got_429=true
        retry_after=$(get_header x-ratelimit-retry-after)
        echo -e "  ${YELLOW}→ Request $i: 429 Too Many Requests (retry-after=$retry_after)${NC}"
        break
    else
        echo -e "  → Request $i: $STATUS (remaining=$(get_header x-ratelimit-remaining))"
    fi
done

if $got_429; then
    echo -e "  ${GREEN}PASS${NC} Rate limit triggered 429"
    PASS=$((PASS + 1))
else
    echo -e "  ${RED}FAIL${NC} Rate limit did not trigger 429 after 15 requests"
    FAIL=$((FAIL + 1))
fi

# ─────────────────────────────────────────────────────────────
# Test 3: 429 response includes Retry-After header
# ─────────────────────────────────────────────────────────────
print_header "Test 3: 429 response includes X-Ratelimit-Retry-After"

assert_header_exists "X-Ratelimit-Retry-After on 429" "$retry_after"

# ─────────────────────────────────────────────────────────────
# Test 4: 429 response body is JSON
# ─────────────────────────────────────────────────────────────
print_header "Test 4: 429 response body is valid JSON with error message"

# Exhaust remaining tokens to guarantee a 429 response
for _ in $(seq 1 12); do
    do_request "$BASE_URL/hello" -H "X-Forwarded-For: 10.0.0.2"
    if [[ "$STATUS" == "429" ]]; then break; fi
done
body_429=$(cat "$BODY_FILE")
if echo "$body_429" | python3 -c "import sys,json; d=json.load(sys.stdin); assert 'error' in d" 2>/dev/null; then
    echo -e "  ${GREEN}PASS${NC} Body contains JSON error field ($body_429)"
    PASS=$((PASS + 1))
else
    echo -e "  ${RED}FAIL${NC} Body is not valid JSON with error field ($body_429)"
    FAIL=$((FAIL + 1))
fi

# ─────────────────────────────────────────────────────────────
# Test 5: Different clients have independent rate limits
# ─────────────────────────────────────────────────────────────
print_header "Test 5: Different clients have independent rate limits"

do_request "$BASE_URL/hello" -H "X-Forwarded-For: 10.0.0.10"
status_a="$STATUS"
do_request "$BASE_URL/hello" -H "X-Forwarded-For: 10.0.0.11"
status_b="$STATUS"

assert_status "Client A gets 200" "200" "$status_a"
assert_status "Client B gets 200" "200" "$status_b"

# ─────────────────────────────────────────────────────────────
# Test 6: Unmatched endpoint uses default limits
# ─────────────────────────────────────────────────────────────
print_header "Test 6: GET /unknown — default rate limit (no matching rule)"

do_request "$BASE_URL/unknown-endpoint" -H "X-Forwarded-For: 10.0.0.20"
assert_status "Unmatched endpoint returns 404" "404" "$STATUS"
limit=$(get_header x-ratelimit-limit)
assert_header_exists "Default rate limit header present" "$limit"
echo -e "  → Default limit value: $limit"

# ─────────────────────────────────────────────────────────────
# Test 7: Client identification via X-Forwarded-For
# ─────────────────────────────────────────────────────────────
print_header "Test 7: Client identified by X-Forwarded-For header"

do_request "$BASE_URL/hello" -H "X-Forwarded-For: 203.0.113.42"
assert_status "X-Forwarded-For client gets 200" "200" "$STATUS"
assert_header_exists "Rate limit tracked for forwarded IP" "$(get_header x-ratelimit-remaining)"

# ─────────────────────────────────────────────────────────────
# Test 8: Sliding window counter endpoint (/api/default, 5 req/s)
# ─────────────────────────────────────────────────────────────
print_header "Test 8: GET /api/default — sliding window counter (5 req/s)"

got_429=false
for i in $(seq 1 10); do
    do_request "$BASE_URL/api/default" -H "X-Forwarded-For: 10.0.0.30"
    if [[ "$STATUS" == "429" ]]; then
        got_429=true
        echo -e "  ${YELLOW}→ Request $i: 429 Too Many Requests${NC}"
        break
    else
        echo -e "  → Request $i: $STATUS (remaining=$(get_header x-ratelimit-remaining))"
    fi
done

if $got_429; then
    echo -e "  ${GREEN}PASS${NC} Sliding window counter triggered 429"
    PASS=$((PASS + 1))
else
    echo -e "  ${RED}FAIL${NC} Sliding window counter did not trigger 429"
    FAIL=$((FAIL + 1))
fi

# ─────────────────────────────────────────────────────────────
# Test 9: Concurrent requests from same client
# ─────────────────────────────────────────────────────────────
print_header "Test 9: Concurrent requests (burst of 10 in parallel)"

tmpdir=$(mktemp -d)
for i in $(seq 1 10); do
    curl -s -o /dev/null -w "%{http_code}" \
        "$BASE_URL/hello" -H "X-Forwarded-For: 10.0.0.40" > "$tmpdir/status-$i.txt" &
done
wait

count_200=0
count_429=0
for i in $(seq 1 10); do
    s=$(cat "$tmpdir/status-$i.txt")
    if [[ "$s" == "200" ]]; then count_200=$((count_200 + 1)); fi
    if [[ "$s" == "429" ]]; then count_429=$((count_429 + 1)); fi
done
rm -rf "$tmpdir"

echo -e "  → 200 responses: $count_200"
echo -e "  → 429 responses: $count_429"
total=$((count_200 + count_429))
if [[ "$total" -eq 10 ]]; then
    echo -e "  ${GREEN}PASS${NC} All 10 requests returned 200 or 429"
    PASS=$((PASS + 1))
else
    echo -e "  ${RED}FAIL${NC} Unexpected status codes in concurrent batch"
    FAIL=$((FAIL + 1))
fi

# ─────────────────────────────────────────────────────────────
# Test 10: Load balancer distributes across API instances
# ─────────────────────────────────────────────────────────────
print_header "Test 10: Nginx load balancer health check"

do_request "$BASE_URL/hello" -H "X-Forwarded-For: 10.0.0.50"
assert_status "Request through load balancer succeeds" "200" "$STATUS"
echo -e "  → Server header: $(get_header server)"

# ─────────────────────────────────────────────────────────────
# Summary
# ─────────────────────────────────────────────────────────────
print_header "Results"

TOTAL=$((PASS + FAIL))
echo -e "  ${GREEN}Passed: $PASS${NC}"
echo -e "  ${RED}Failed: $FAIL${NC}"
echo -e "  Total:  $TOTAL"
echo ""

if [[ "$FAIL" -gt 0 ]]; then
    exit 1
fi
