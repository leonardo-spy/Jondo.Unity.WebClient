#!/bin/bash
# Iteratively fix Core.dll Cpp2IL by building, finding broken methods,
# adding them to the fix list, and rebuilding until the build passes.
#
# Usage: bash Tools/FixComObject/fix_iterative.sh

REPO_DIR="$(cd "$(dirname "$0")/../.." && pwd)"
TOOL_DIR="$REPO_DIR/Tools/FixComObject"
CPP2IL_CORE="$REPO_DIR/Cpp2IL_Output/Core.dll"
CPP2IL_BACKUP="$REPO_DIR/Cpp2IL_Output/Core.dll.bak"
UNITY_CORE="C:/DofusWeb/Assets/Plugins/Core.dll"
BUILD_LOG="C:/Users/Leona/AppData/Local/Temp/unity_build_iter.log"
BROKEN_LIST="$TOOL_DIR/broken_methods.txt"

# Start fresh
cp "$CPP2IL_BACKUP" "$CPP2IL_CORE"
> "$BROKEN_LIST"

MAX_ITERATIONS=20
for i in $(seq 1 $MAX_ITERATIONS); do
    echo "=== Iteration $i ==="

    # Apply all fixes (ComObject + broken methods)
    cd "$TOOL_DIR"
    dotnet run -- "$CPP2IL_CORE" 2>&1 | tail -3

    # Copy to Unity project
    cp "$CPP2IL_CORE" "$UNITY_CORE"

    # Clear linker cache
    rm -rf "C:/DofusWeb/Library/Bee/artifacts/WebGL" 2>/dev/null
    rm -rf "C:/DofusWeb/Library/Bee/artifacts/unitylinker"* 2>/dev/null

    # Build
    taskkill //F //IM Unity.exe 2>/dev/null
    sleep 2
    rm -f "C:/Users/Leona/Documents/Projetos/JondoEmu/PlayBrowser/WebClient/unity-build/Build/"*
    "/c/Program Files/Unity/Hub/Editor/6000.3.16f1/Editor/Unity.exe" \
        -batchmode -nographics -projectPath "C:/DofusWeb" \
        -executeMethod WebGLBuildMenu.BuildWebGL -quit \
        -logFile "$BUILD_LOG" 2>&1

    # Wait
    while tasklist 2>/dev/null | grep -q "Unity.exe"; do sleep 10; done

    # Check result
    if ls "C:/Users/Leona/Documents/Projetos/JondoEmu/PlayBrowser/WebClient/unity-build/Build/"*.wasm.gz 2>/dev/null; then
        echo "BUILD SUCCESS at iteration $i!"
        break
    fi

    # Extract broken method from error
    BROKEN=$(grep "error IL1005" "$BUILD_LOG" 2>/dev/null | head -1 | sed 's/.*IL1005: \(.*\)(): Error.*/\1/')
    if [ -z "$BROKEN" ]; then
        echo "Unknown error — not an IL1005"
        grep "error" "$BUILD_LOG" 2>/dev/null | grep -v "Licensing\|gRPC\|Microsoft" | head -3
        break
    fi

    echo "  Broken: $BROKEN"
    echo "$BROKEN" >> "$BROKEN_LIST"

    # Restore fresh copy and re-apply with new broken method
    cp "$CPP2IL_BACKUP" "$CPP2IL_CORE"
done

echo ""
echo "Broken methods found:"
cat "$BROKEN_LIST" 2>/dev/null
echo ""
echo "Total: $(wc -l < "$BROKEN_LIST" 2>/dev/null) methods"
