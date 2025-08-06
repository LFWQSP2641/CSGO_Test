# GoTest 项目构建 Makefile
# 支持 Windows、Linux、macOS 跨平台构建

# 项目配置
PROJECT_NAME = GoTest
GO_MODULE = gocsinterop
PROTO_DIR = proto
GO_DIR = go
CS_DIR = GoPInvokeDemo

# 构建配置
BUILD_MODE = c-shared
GO_OUTPUT = add
CS_OUTPUT = GoPInvokeDemo

# 平台检测
ifeq ($(OS),Windows_NT)
    PLATFORM = windows
    DLL_EXT = .dll
    EXE_EXT = .exe
    RM = del /Q
    MKDIR = mkdir
    COPY = copy
    MOVE = move
    SEP = \\
else
    UNAME_S := $(shell uname -s)
    ifeq ($(UNAME_S),Linux)
        PLATFORM = linux
        DLL_EXT = .so
        EXE_EXT = 
        RM = rm -f
        MKDIR = mkdir -p
        COPY = cp
        MOVE = mv
        SEP = /
    endif
    ifeq ($(UNAME_S),Darwin)
        PLATFORM = darwin
        DLL_EXT = .dylib
        EXE_EXT = 
        RM = rm -f
        MKDIR = mkdir -p
        COPY = cp
        MOVE = mv
        SEP = /
    endif
endif

# 路径配置
PROTO_FILES = $(PROTO_DIR)/messages.proto
GO_PROTO_OUT = $(GO_DIR)/messages.pb.go
CS_PROTO_OUT = $(CS_DIR)/obj/Debug/net9.0/Messages.cs

# Go 构建配置
GO_SRC = $(GO_DIR)$(SEP)add.go $(GO_PROTO_OUT)
GO_OUTPUT_DEBUG = $(GO_DIR)$(SEP)$(GO_OUTPUT)_debug$(DLL_EXT)
GO_OUTPUT_RELEASE = $(GO_DIR)$(SEP)$(GO_OUTPUT)$(DLL_EXT)

# C# 构建配置
CS_OUTPUT_DIR_DEBUG = $(CS_DIR)$(SEP)bin$(SEP)Debug$(SEP)net9.0
CS_OUTPUT_DIR_RELEASE = $(CS_DIR)$(SEP)bin$(SEP)Release$(SEP)net9.0
CS_EXE_DEBUG = $(CS_OUTPUT_DIR_DEBUG)$(SEP)$(CS_OUTPUT)$(EXE_EXT)
CS_EXE_RELEASE = $(CS_OUTPUT_DIR_RELEASE)$(SEP)$(CS_OUTPUT)$(EXE_EXT)

# 默认目标
.PHONY: all clean debug release proto go-debug go-release cs-debug cs-release install-deps help

all: debug

help:
	@echo "GoTest 项目构建系统"
	@echo "===================="
	@echo "可用目标:"
	@echo "  all          - 构建 debug 版本 (默认)"
	@echo "  debug        - 构建 debug 版本"
	@echo "  release      - 构建 release 版本"
	@echo "  proto        - 生成 protobuf 代码"
	@echo "  go-debug     - 仅构建 Go debug 版本"
	@echo "  go-release   - 仅构建 Go release 版本"
	@echo "  cs-debug     - 仅构建 C# debug 版本"
	@echo "  cs-release   - 仅构建 C# release 版本"
	@echo "  clean        - 清理构建文件"
	@echo "  install-deps - 安装依赖"
	@echo "  help         - 显示此帮助"
	@echo ""
	@echo "平台: $(PLATFORM)"

# 安装依赖
install-deps:
	@echo "安装 Go 依赖..."
	cd $(GO_DIR) && go mod tidy
	@echo "安装 protobuf 编译器工具..."
	go install google.golang.org/protobuf/cmd/protoc-gen-go@latest
	@echo "恢复 C# 依赖..."
	cd $(CS_DIR) && dotnet restore

# 生成 protobuf 代码
proto: $(GO_PROTO_OUT)

$(GO_PROTO_OUT): $(PROTO_FILES)
	@echo "生成 protobuf 代码..."
	@echo "  Go 代码生成..."
	protoc --proto_path=$(PROTO_DIR) --go_out=$(GO_DIR) --go_opt=paths=source_relative messages.proto

# Debug 构建
debug: go-debug cs-debug
	@echo "Debug 构建完成!"
	@echo "Go DLL: $(GO_OUTPUT_DEBUG)"
	@echo "C# EXE: $(CS_EXE_DEBUG)"

# Release 构建
release: go-release cs-release
	@echo "Release 构建完成!"
	@echo "Go DLL: $(GO_OUTPUT_RELEASE)"
	@echo "C# EXE: $(CS_EXE_RELEASE)"

# Go Debug 构建
go-debug: $(GO_OUTPUT_DEBUG)

$(GO_OUTPUT_DEBUG): $(GO_SRC)
	@echo "构建 Go Debug 版本..."
	cd $(GO_DIR) && go build -buildmode=$(BUILD_MODE) -gcflags="all=-N -l" -ldflags="-X main.BuildMode=debug" -o $(GO_OUTPUT)_debug$(DLL_EXT) add.go messages.pb.go
	@echo "Go Debug 构建完成: $(GO_OUTPUT_DEBUG)"

# Go Release 构建
go-release: $(GO_OUTPUT_RELEASE)

$(GO_OUTPUT_RELEASE): $(GO_SRC)
	@echo "构建 Go Release 版本..."
	cd $(GO_DIR) && go build -buildmode=$(BUILD_MODE) -ldflags="-s -w -X main.BuildMode=release" -o $(GO_OUTPUT)$(DLL_EXT) add.go messages.pb.go
	@echo "Go Release 构建完成: $(GO_OUTPUT_RELEASE)"

# C# Debug 构建
cs-debug: proto $(CS_EXE_DEBUG)

$(CS_EXE_DEBUG): $(GO_OUTPUT_DEBUG)
	@echo "构建 C# Debug 版本..."
	cd $(CS_DIR) && dotnet build --configuration Debug
	@echo "复制 Go 库到 C# 输出目录..."
	$(COPY) "$(GO_OUTPUT_DEBUG)" "$(CS_OUTPUT_DIR_DEBUG)$(SEP)$(GO_OUTPUT)$(DLL_EXT)"
	@echo "C# Debug 构建完成: $(CS_EXE_DEBUG)"

# C# Release 构建
cs-release: proto $(CS_EXE_RELEASE)

$(CS_EXE_RELEASE): $(GO_OUTPUT_RELEASE)
	@echo "构建 C# Release 版本..."
	cd $(CS_DIR) && dotnet build --configuration Release
	@echo "复制 Go 库到 C# 输出目录..."
	$(COPY) "$(GO_OUTPUT_RELEASE)" "$(CS_OUTPUT_DIR_RELEASE)$(SEP)$(GO_OUTPUT)$(DLL_EXT)"
	@echo "C# Release 构建完成: $(CS_EXE_RELEASE)"

# 清理
clean:
	@echo "清理构建文件..."
	-$(RM) $(GO_DIR)$(SEP)*$(DLL_EXT)
	-$(RM) $(GO_DIR)$(SEP)*.h
	-$(RM) $(GO_DIR)$(SEP)messages.pb.go
	-$(RM) "$(CS_OUTPUT_DIR_DEBUG)$(SEP)$(GO_OUTPUT)$(DLL_EXT)"
	-$(RM) "$(CS_OUTPUT_DIR_RELEASE)$(SEP)$(GO_OUTPUT)$(DLL_EXT)"
	cd $(CS_DIR) && dotnet clean
	@echo "清理完成"

# 测试目标
test-debug: debug
	@echo "运行 Debug 版本测试..."
ifeq ($(OS),Windows_NT)
	cd $(CS_OUTPUT_DIR_DEBUG) && $(CS_OUTPUT)$(EXE_EXT)
else
	cd $(CS_OUTPUT_DIR_DEBUG) && ./$(CS_OUTPUT)$(EXE_EXT)
endif

test-release: release
	@echo "运行 Release 版本测试..."
ifeq ($(OS),Windows_NT)
	cd $(CS_OUTPUT_DIR_RELEASE) && $(CS_OUTPUT)$(EXE_EXT)
else
	cd $(CS_OUTPUT_DIR_RELEASE) && ./$(CS_OUTPUT)$(EXE_EXT)
endif

# 打包
package-debug: debug
	@echo "打包 Debug 版本..."
	$(MKDIR) dist$(SEP)debug
ifeq ($(OS),Windows_NT)
	$(COPY) "$(CS_OUTPUT_DIR_DEBUG)$(SEP)*" "dist$(SEP)debug$(SEP)"
else
	$(COPY) $(CS_OUTPUT_DIR_DEBUG)/* dist/debug/
endif

package-release: release
	@echo "打包 Release 版本..."
	$(MKDIR) dist$(SEP)release
ifeq ($(OS),Windows_NT)
	$(COPY) "$(CS_OUTPUT_DIR_RELEASE)$(SEP)*" "dist$(SEP)release$(SEP)"
else
	$(COPY) $(CS_OUTPUT_DIR_RELEASE)/* dist/release/
endif

# 持续集成目标
ci: clean install-deps proto debug release
	@echo "持续集成构建完成!"
