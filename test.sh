#!/bin/bash
set -e
cd "$(dirname "$0")/Tests/SearchFix.Tests"
/home/deck/.dotnet/dotnet test "$@"
