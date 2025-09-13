using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Rythmos.Handlers
{
    internal class Queue
    {
        private static readonly ConcurrentQueue<byte[]> Q = new();
        private static readonly SemaphoreSlim Signal = new(0);

        private static NetworkStream? S;
        private static CancellationTokenSource? Token;
        private static Task? Writer;

        public static void Start(NetworkStream Stream)
        {
            Stop();
            S = Stream;
            Token = new CancellationTokenSource();
            Writer = Task.Run(() => Process(Token.Token));
        }

        public static void Send(byte[] Data, byte Type)
        {
            if (S is null) return;
            byte[] Output = new byte[Data.Length + 6];
            var Size = Data.Length;
            var E = (byte)(Size % 256);
            Size -= E;
            Size /= 256;
            var D = (byte)(Size % 256);
            Size -= D;
            Size /= 256;
            var C = (byte)(Size % 256);
            Size -= C;
            Size /= 256;
            var B = (byte)(Size % 256);
            Size -= B;
            Size /= 256;
            var A = (byte)(Size % 256);
            Output[0] = A;
            Output[1] = B;
            Output[2] = C;
            Output[3] = D;
            Output[4] = E;
            Output[5] = Type;
            for (var I = 0; I < Data.Length; I++) Output[I + 6] = Data[I];
            Q.Enqueue(Output);
            Signal.Release();
        }

        private static async Task Process(CancellationToken T)
        {
            while (!T.IsCancellationRequested && S != null)
            {
                try
                {
                    await Signal.WaitAsync(T);
                    if (T.IsCancellationRequested) break;
                    if (Q.TryDequeue(out var Frame))
                    {
                        await S.WriteAsync(Frame, 0, Frame.Length, T);
                        await S.FlushAsync(T);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception Error)
                {
                    break;
                }
            }
        }

        public static void Stop()
        {
            try
            {
                Token?.Cancel();
                Writer?.Wait();
            }
            catch { }
            finally
            {
                while (Q.TryDequeue(out _)) { }
                Token?.Dispose();
                Token = null;
                Writer = null;
                S = null;
            }
        }
    }
}
