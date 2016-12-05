using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Text;

using Xunit;

using Http2;

namespace Http2Tests
{
    public class ClientPrefaceTests
    {
        [Fact]
        public void PrefixShouldBy24BytesLong()
        {
            Assert.Equal(24, ClientPreface.Length);
        }

        [Fact]
        public async Task ShouldWriteThePrefaceToStream()
        {
            var buffer = new BufferWriteStream(50);
            await ClientPreface.WriteAsync(buffer);
            Assert.Equal(ClientPreface.Length, buffer.Written);

            var pf = Encoding.ASCII.GetString(buffer.Buffer, 0, ClientPreface.Length);
            Assert.Equal(ClientPreface.String, pf);
        }

        [Fact]
        public async Task ShouldReadThePrefaceFromStream()
        {
            var buffer = new BufferReadStream(50, 50);
            Array.Copy(ClientPreface.Bytes, buffer.Buffer, ClientPreface.Length);
            buffer.Written = ClientPreface.Length;
            await ClientPreface.ReadAsync(buffer);
        }

        [Fact]
        public async Task ShouldErrorIfStreamEnds()
        {
            var buffer = new BufferReadStream(50, 50);
            Array.Copy(ClientPreface.Bytes, buffer.Buffer, ClientPreface.Length);
            buffer.Written = ClientPreface.Length - 1; // Miss one byte
            await Assert.ThrowsAsync<EndOfStreamException>(
                () => ClientPreface.ReadAsync(buffer).AsTask());
        }

        [Fact]
        public async Task ShouldErrorIfStreamDoesNotContainPreface()
        {
            var buffer = new BufferReadStream(50, 50);
            Array.Copy(ClientPreface.Bytes, buffer.Buffer, ClientPreface.Length);
            ClientPreface.Bytes[22] = (byte)'l';
            buffer.Written = ClientPreface.Length;
            var ex = await Assert.ThrowsAsync<Exception>(
                () => ClientPreface.ReadAsync(buffer).AsTask());
            Assert.Equal("Invalid prefix received", ex.Message);
        }

        [Fact]
        public async Task ShouldErrorIfPrefaceWasNotReceivedUntilTimeout()
        {
            var timout = 50;
            var pipe = new BufferedPipe(50);
            await Assert.ThrowsAsync<TimeoutException>(
                () => ClientPreface.ReadAsync(pipe, timout).AsTask());
        }

        [Fact]
        public async Task ShouldNotErrorIfPrefaceWasReceivedWithinTimeout()
        {
            var timout = 100;
            var pipe = new BufferedPipe(50);
            var _ = Task.Run(async () =>
            {
                await Task.Delay(10);
                await pipe.WriteAsync(new ArraySegment<byte>(ClientPreface.Bytes));
            });
            await ClientPreface.ReadAsync(pipe, timout);
        }

        [Fact]
        public async Task ShouldErrorIfStreamWasClosedWithinTimeout()
        {
            var timout = 100;
            var pipe = new BufferedPipe(50);
            var _ = Task.Run(async () =>
            {
                await Task.Delay(10);
                await pipe.CloseAsync();
            });
            var ex = await Assert.ThrowsAsync<AggregateException>(
                () => ClientPreface.ReadAsync(pipe, timout).AsTask());
            Assert.IsType<EndOfStreamException>(ex.InnerException);
        }
    }
}