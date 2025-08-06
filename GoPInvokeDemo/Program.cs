using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Gocsinterop;

public class Program
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void MyCallbackDelegate(IntPtr handle, double result);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void StringListCallbackDelegate(IntPtr handle, IntPtr stringArray, int count);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ProtobufCallbackDelegate(IntPtr handle, IntPtr data, int length);

    [DllImport("add.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void run_task_async(double input, MyCallbackDelegate callback, IntPtr handle);

    [DllImport("add.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void process_string_async([MarshalAs(UnmanagedType.LPStr)] string input, 
        StringListCallbackDelegate callback, IntPtr handle);

    [DllImport("add.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void process_protobuf_async(IntPtr data, int length, 
        ProtobufCallbackDelegate callback, IntPtr handle);

    public static async Task Main(string[] args)
    {
        Console.WriteLine($"C#: 主线程 ID: {Thread.CurrentThread.ManagedThreadId}");
        
        try
        {
            Console.WriteLine("C#: 调用 Go 浮点数异步函数...");
            double result = await RunTaskAsync();
            Console.WriteLine($"C#: 浮点数异步操作完成，结果是: {result:F6}");
            
            Console.WriteLine();
            
            Console.WriteLine("C#: 调用 Go 字符串处理异步函数...");
            List<string> stringResults = await ProcessStringAsync("Hello World");
            Console.WriteLine($"C#: 字符串处理完成，返回 {stringResults.Count} 个字符串:");
            for (int i = 0; i < stringResults.Count; i++)
            {
                Console.WriteLine($"C#:   [{i}] = \"{stringResults[i]}\"");
            }

            Console.WriteLine();
            
            Console.WriteLine("C#: 调用 Go Protobuf 处理异步函数...");
            ProcessResponse protobufResult = await ProcessProtobufAsync();
            Console.WriteLine($"C#: Protobuf 处理完成:");
            Console.WriteLine($"C#:   成功: {protobufResult.Success}");
            Console.WriteLine($"C#:   计算值: {protobufResult.CalculatedValue:F6}");
            Console.WriteLine($"C#:   时间戳: {protobufResult.Timestamp}");
            Console.WriteLine($"C#:   结果字符串数量: {protobufResult.ResultStrings.Count}");
            foreach (var str in protobufResult.ResultStrings)
            {
                Console.WriteLine($"C#:     - \"{str}\"");
            }
            
            Console.WriteLine($"C#: 完成时主线程 ID: {Thread.CurrentThread.ManagedThreadId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"C#: 异步操作发生错误: {ex.Message}");
        }
    }

    private static Task<double> RunTaskAsync()
    {
        Console.WriteLine($"C#: RunTaskAsync 在线程 {Thread.CurrentThread.ManagedThreadId} 上调用");
        
        var tcs = new TaskCompletionSource<double>();
        
        var handle = GCHandle.Alloc(tcs);
        
        try
        {
            MyCallbackDelegate callback = OnTaskCompleted;
            
            Console.WriteLine($"C#: 即将调用 Go 函数，当前线程: {Thread.CurrentThread.ManagedThreadId}");
            
            run_task_async(256.0, callback, GCHandle.ToIntPtr(handle));
            
            Console.WriteLine($"C#: Go 函数调用已返回，当前线程: {Thread.CurrentThread.ManagedThreadId}");
            
            return tcs.Task;
        }
        catch
        {
            handle.Free();
            throw;
        }
    }

    private static Task<List<string>> ProcessStringAsync(string input)
    {
        Console.WriteLine($"C#: ProcessStringAsync 在线程 {Thread.CurrentThread.ManagedThreadId} 上调用");
        
        var tcs = new TaskCompletionSource<List<string>>();
        
        var handle = GCHandle.Alloc(tcs);
        
        try
        {
            StringListCallbackDelegate callback = OnStringListCompleted;
            
            Console.WriteLine($"C#: 即将调用 Go 字符串处理函数，输入: \"{input}\"");
            
            process_string_async(input, callback, GCHandle.ToIntPtr(handle));
            
            Console.WriteLine($"C#: Go 字符串函数调用已返回，当前线程: {Thread.CurrentThread.ManagedThreadId}");
            
            return tcs.Task;
        }
        catch
        {
            handle.Free();
            throw;
        }
    }

    private static void OnTaskCompleted(IntPtr handle, double result)
    {
        Console.WriteLine($"C#: 回调在线程 {Thread.CurrentThread.ManagedThreadId} 上执行");
        
        GCHandle gcHandle = default;
        try
        {
            gcHandle = GCHandle.FromIntPtr(handle);
            
            if (gcHandle.Target is TaskCompletionSource<double> tcs)
            {
                Console.WriteLine($"C#: 回调函数被 Go 协程调用了！结果是: {result:F6}");
                
                tcs.SetResult(result);
            }
        }
        catch (Exception ex)
        {
            if (gcHandle.IsAllocated && gcHandle.Target is TaskCompletionSource<double> tcs)
            {
                tcs.SetException(ex);
            }
        }
        finally
        {
            if (gcHandle.IsAllocated)
            {
                gcHandle.Free();
            }
        }
    }

    private static void OnStringListCompleted(IntPtr handle, IntPtr stringArray, int count)
    {
        Console.WriteLine($"C#: 字符串列表回调在线程 {Thread.CurrentThread.ManagedThreadId} 上执行，收到 {count} 个字符串");
        
        GCHandle gcHandle = default;
        try
        {
            gcHandle = GCHandle.FromIntPtr(handle);
            
            if (gcHandle.Target is TaskCompletionSource<List<string>> tcs)
            {
                var stringList = new List<string>();
                
                IntPtr[] stringPointers = new IntPtr[count];
                Marshal.Copy(stringArray, stringPointers, 0, count);
                
                for (int i = 0; i < count; i++)
                {
                    string? str = Marshal.PtrToStringAnsi(stringPointers[i]);
                    if (str != null)
                    {
                        stringList.Add(str);
                        Console.WriteLine($"C#: 收到字符串 [{i}]: \"{str}\"");
                    }
                }
                
                Console.WriteLine($"C#: 字符串列表回调完成，总共处理了 {stringList.Count} 个字符串");
                
                tcs.SetResult(stringList);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"C#: 字符串回调处理异常: {ex.Message}");
            if (gcHandle.IsAllocated && gcHandle.Target is TaskCompletionSource<List<string>> tcs)
            {
                tcs.SetException(ex);
            }
        }
        finally
        {
            if (gcHandle.IsAllocated)
            {
                gcHandle.Free();
            }
        }
    }

    private static Task<ProcessResponse> ProcessProtobufAsync()
    {
        Console.WriteLine($"C#: ProcessProtobufAsync 在线程 {Thread.CurrentThread.ManagedThreadId} 上调用");
        
        var tcs = new TaskCompletionSource<ProcessResponse>();
        
        var handle = GCHandle.Alloc(tcs);
        
        try
        {
            var request = new ProcessRequest
            {
                InputText = "Protobuf测试",
                NumberValue = 3.14159,
                Tags = { "tag1", "tag2", "protobuf" }
            };
            
            Console.WriteLine($"C#: 创建 Protobuf 请求，大小: {request.CalculateSize()} 字节");
            
            byte[] requestData = request.ToByteArray();
            
            IntPtr dataPtr = Marshal.AllocHGlobal(requestData.Length);
            Marshal.Copy(requestData, 0, dataPtr, requestData.Length);
            
            ProtobufCallbackDelegate callback = OnProtobufCompleted;
            
            Console.WriteLine($"C#: 即将调用 Go Protobuf 函数，数据长度: {requestData.Length}");
            
            process_protobuf_async(dataPtr, requestData.Length, callback, GCHandle.ToIntPtr(handle));
            
            Marshal.FreeHGlobal(dataPtr);
            
            Console.WriteLine($"C#: Go Protobuf 函数调用已返回，当前线程: {Thread.CurrentThread.ManagedThreadId}");
            
            return tcs.Task;
        }
        catch
        {
            handle.Free();
            throw;
        }
    }

    private static void OnProtobufCompleted(IntPtr handle, IntPtr data, int length)
    {
        Console.WriteLine($"C#: Protobuf 回调在线程 {Thread.CurrentThread.ManagedThreadId} 上执行，收到 {length} 字节数据");
        
        GCHandle gcHandle = default;
        try
        {
            gcHandle = GCHandle.FromIntPtr(handle);
            
            if (gcHandle.Target is TaskCompletionSource<ProcessResponse> tcs)
            {
                byte[] responseData = new byte[length];
                Marshal.Copy(data, responseData, 0, length);
                
                ProcessResponse response = ProcessResponse.Parser.ParseFrom(responseData);
                
                Console.WriteLine($"C#: Protobuf 响应解析完成，包含 {response.ResultStrings.Count} 个字符串");
                
                tcs.SetResult(response);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"C#: Protobuf 回调处理异常: {ex.Message}");
            if (gcHandle.IsAllocated && gcHandle.Target is TaskCompletionSource<ProcessResponse> tcs)
            {
                tcs.SetException(ex);
            }
        }
        finally
        {
            if (gcHandle.IsAllocated)
            {
                gcHandle.Free();
            }
        }
    }
}