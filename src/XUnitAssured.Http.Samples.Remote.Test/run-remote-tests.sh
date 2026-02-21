#!/bin/bash
# run-remote-tests.sh
# Bash script to run XUnitAssured remote tests with environment configuration

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Default values
ENVIRONMENT="local"
API_URL=""
API_TOKEN=""
FILTER=""
VERBOSE=""

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -e|--environment)
            ENVIRONMENT="$2"
            shift 2
            ;;
        -u|--url)
            API_URL="$2"
            shift 2
            ;;
        -t|--token)
            API_TOKEN="$2"
            shift 2
            ;;
        -f|--filter)
            FILTER="$2"
            shift 2
            ;;
        -v|--verbose)
            VERBOSE="-v detailed"
            shift
            ;;
        -h|--help)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  -e, --environment ENV    Target environment: local, staging, prod (default: local)"
            echo "  -u, --url URL           Override API URL"
            echo "  -t, --token TOKEN       API authentication token"
            echo "  -f, --filter FILTER     Test filter expression"
            echo "  -v, --verbose           Enable verbose output"
            echo "  -h, --help              Show this help message"
            echo ""
            echo "Examples:"
            echo "  $0                                          # Run with default local config"
            echo "  $0 -e staging -u https://api.staging.com   # Run against staging"
            echo "  $0 -e staging -f \"Category=Integration\"    # Run integration tests only"
            echo "  $0 -e prod -t xyz... -v                    # Run against prod with auth"
            exit 0
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            exit 1
            ;;
    esac
done

# Validate environment
if [[ ! "$ENVIRONMENT" =~ ^(local|staging|prod)$ ]]; then
    echo -e "${RED}Invalid environment: $ENVIRONMENT${NC}"
    echo "Valid options: local, staging, prod"
    exit 1
fi

echo -e "${CYAN}🚀 XUnitAssured Remote Tests Runner${NC}"
echo -e "${YELLOW}Environment: $ENVIRONMENT${NC}"

# Set environment variables
export TEST_ENV="$ENVIRONMENT"

# Set API URL
if [ -n "$API_URL" ]; then
    case $ENVIRONMENT in
        local)
            export REMOTE_API_URL="$API_URL"
            ;;
        staging)
            export STAGING_API_URL="$API_URL"
            ;;
        prod)
            export PROD_API_URL="$API_URL"
            ;;
    esac
    echo -e "${GREEN}API URL: $API_URL${NC}"
fi

# Set API token
if [ -n "$API_TOKEN" ]; then
    case $ENVIRONMENT in
        staging)
            export STAGING_API_TOKEN="$API_TOKEN"
            ;;
        prod)
            export PROD_API_TOKEN="$API_TOKEN"
            ;;
    esac
    echo -e "${GREEN}✅ API Token configured${NC}"
fi

# Build filter argument
FILTER_ARG=""
if [ -n "$FILTER" ]; then
    FILTER_ARG="--filter \"$FILTER\""
    echo -e "${YELLOW}Test Filter: $FILTER${NC}"
fi

# Navigate to script directory
cd "$(dirname "$0")"

echo -e "\n${CYAN}📋 Running tests...${NC}"

# Run tests
if [ -n "$FILTER" ]; then
    dotnet test $VERBOSE --filter "$FILTER" --logger "console;verbosity=normal"
else
    dotnet test $VERBOSE --logger "console;verbosity=normal"
fi

# Check exit code
EXIT_CODE=$?
if [ $EXIT_CODE -eq 0 ]; then
    echo -e "\n${GREEN}✅ All tests passed!${NC}"
else
    echo -e "\n${RED}❌ Some tests failed!${NC}"
fi

# Cleanup environment variables
unset TEST_ENV
unset REMOTE_API_URL
unset STAGING_API_URL
unset PROD_API_URL
unset STAGING_API_TOKEN
unset PROD_API_TOKEN

exit $EXIT_CODE
