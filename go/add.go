package main

/*
#include <stdlib.h>
#include <pthread.h>
#include <stdio.h>
#include <string.h>

typedef void (*CallbackFunc)(void* handle, double result);
typedef void (*StringListCallbackFunc)(void* handle, char** stringArray, int count);
typedef void (*ProtobufCallbackFunc)(void* handle, void* data, int length);

static void invoke_callback_func(CallbackFunc callback, void* handle, double result) {
    if (callback != NULL) {
        printf("Go: 回调在线程 %lu 上执行\n", pthread_self());
        callback(handle, result);
    }
}

static void invoke_string_list_callback(StringListCallbackFunc callback, void* handle, char** stringArray, int count) {
    if (callback != NULL) {
        printf("Go: 字符串列表回调在线程 %lu 上执行，返回 %d 个字符串\n", pthread_self(), count);
        callback(handle, stringArray, count);
    }
}

static void invoke_protobuf_callback(ProtobufCallbackFunc callback, void* handle, void* data, int length) {
    if (callback != NULL) {
        printf("Go: Protobuf 回调在线程 %lu 上执行，返回 %d 字节数据\n", pthread_self(), length);
        callback(handle, data, length);
    }
}
*/
import "C"
import (
	"fmt"
	"math"
	"runtime"
	"time"
	"unsafe"

	"google.golang.org/protobuf/proto"
)

//export run_task_async
func run_task_async(input C.double, callback C.CallbackFunc, handle unsafe.Pointer) {
	fmt.Printf("Go: run_task_async 在线程 %d 上被调用，输入值: %f\n", runtime.NumGoroutine(), float64(input))

	go func() {
		fmt.Printf("Go: 协程在 %d 个系统线程上运行\n", runtime.NumGoroutine())
		fmt.Println("Go: 协程开始执行任务...")

		result := math.Sqrt(float64(input))

		fmt.Printf("Go: 协程任务完成，输入: %f, 开方结果: %f\n", float64(input), result)
		fmt.Printf("Go: 当前有 %d 个活跃协程\n", runtime.NumGoroutine())

		C.invoke_callback_func(callback, handle, C.double(result))
	}()

	fmt.Println("Go: run_task_async 立即返回，不阻塞调用线程")
}

//export process_string_async
func process_string_async(input *C.char, callback C.StringListCallbackFunc, handle unsafe.Pointer) {
	inputStr := C.GoString(input)
	fmt.Printf("Go: process_string_async 被调用，输入字符串: %s\n", inputStr)

	go func() {
		fmt.Println("Go: 字符串处理协程开始执行...")

		results := []string{inputStr, inputStr}
		fmt.Printf("Go: 创建字符串列表，包含 %d 个元素\n", len(results))

		cStringArray := C.malloc(C.size_t(len(results)) * C.size_t(unsafe.Sizeof(uintptr(0))))
		defer C.free(cStringArray)

		cStringPtrArray := (*[1 << 30]*C.char)(cStringArray)[:len(results):len(results)]

		for i, str := range results {
			cStr := C.CString(str)
			cStringPtrArray[i] = cStr
		}

		fmt.Printf("Go: 准备调用 C# 回调，返回 %d 个字符串\n", len(results))

		C.invoke_string_list_callback(callback, handle, (**C.char)(cStringArray), C.int(len(results)))

		for i := range results {
			C.free(unsafe.Pointer(cStringPtrArray[i]))
		}

		fmt.Println("Go: 字符串处理协程完成")
	}()

	fmt.Println("Go: process_string_async 立即返回，不阻塞调用线程")
}

//export process_protobuf_async
func process_protobuf_async(data unsafe.Pointer, length C.int, callback C.ProtobufCallbackFunc, handle unsafe.Pointer) {
	fmt.Printf("Go: process_protobuf_async 被调用，数据长度: %d 字节\n", int(length))

	go func() {
		fmt.Println("Go: Protobuf 处理协程开始执行...")

		dataBytes := C.GoBytes(data, length)

		var request ProcessRequest
		err := proto.Unmarshal(dataBytes, &request)
		if err != nil {
			fmt.Printf("Go: 解析 Protobuf 数据失败: %v\n", err)
			response := &ProcessResponse{
				Success:      false,
				ErrorMessage: err.Error(),
				Timestamp:    time.Now().Unix(),
			}
			sendProtobufResponse(callback, handle, response)
			return
		}

		fmt.Printf("Go: 解析 Protobuf 请求成功，输入文本: %s, 数值: %f\n", request.InputText, request.NumberValue)

		time.Sleep(1 * time.Second)

		response := &ProcessResponse{
			ResultStrings:   []string{request.InputText + "_processed", "generated_string", request.InputText + "_final"},
			CalculatedValue: math.Sqrt(request.NumberValue),
			Success:         true,
			ErrorMessage:    "",
			Timestamp:       time.Now().Unix(),
		}

		fmt.Printf("Go: Protobuf 处理完成，计算值: %f\n", response.CalculatedValue)

		sendProtobufResponse(callback, handle, response)
		fmt.Println("Go: Protobuf 处理协程完成")
	}()

	fmt.Println("Go: process_protobuf_async 立即返回，不阻塞调用线程")
}

func sendProtobufResponse(callback C.ProtobufCallbackFunc, handle unsafe.Pointer, response *ProcessResponse) {
	responseBytes, err := proto.Marshal(response)
	if err != nil {
		fmt.Printf("Go: 序列化响应失败: %v\n", err)
		return
	}

	cData := C.malloc(C.size_t(len(responseBytes)))
	defer C.free(cData)

	copy((*[1 << 30]byte)(cData)[:len(responseBytes):len(responseBytes)], responseBytes)

	C.invoke_protobuf_callback(callback, handle, cData, C.int(len(responseBytes)))
}

func main() {}
