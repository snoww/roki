using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Roki.Modules.Utility.Common
{
    // derived from https://gist.github.com/csh/2480d14fbbb33b4bbae3
    public class MinecraftServer
    {
        private readonly TcpClient _client;
        private List<byte> _buffer;
        private int _offset;
        private NetworkStream _stream;

        public MinecraftServer(string host, int port = 25565)
        {
            Host = host;
            Port = port;
            _client = new TcpClient();
        }

        private string Host { get; }
        private int Port { get; }

        public async Task<string> GetStatus()
        {
            await _client.ConnectAsync(Host, Port).ConfigureAwait(false);

            if (!_client.Connected)
            {
                return "Server Offline";
            }

            _buffer = new List<byte>();
            _stream = _client.GetStream();

            Handshake();
            Flush(0);

            var buffer = new byte[short.MaxValue];
            _stream.Read(buffer, 0, buffer.Length);

            try
            {
                int length = ReadVarInt(buffer);
                int packet = ReadVarInt(buffer);
                int jsonLength = ReadVarInt(buffer);
                string json = ReadString(buffer, jsonLength);
                return json;
            }
            catch (IOException)
            {
                return "Server Error";
            }
        }

        private void Handshake()
        {
            WriteVarInt(47);
            WriteString(Host);
            WriteShort((short) Port);
            WriteVarInt(1);
            Flush(0);
        }

        private byte ReadByte(byte[] buffer)
        {
            byte b = buffer[_offset];
            _offset += 1;
            return b;
        }

        private byte[] Read(byte[] buffer, int length)
        {
            var data = new byte[length];
            Array.Copy(buffer, _offset, data, 0, length);
            _offset += length;
            return data;
        }

        private int ReadVarInt(byte[] buffer)
        {
            var value = 0;
            var size = 0;
            int b;
            while (((b = ReadByte(buffer)) & 0x80) == 0x80)
            {
                value |= (b & 0x7F) << (size++ * 7);
                if (size > 5)
                {
                    throw new IOException("This VarInt is an imposter!");
                }
            }

            return value | ((b & 0x7F) << (size * 7));
        }

        private string ReadString(byte[] buffer, int length)
        {
            byte[] data = Read(buffer, length);
            return Encoding.UTF8.GetString(data);
        }

        private void WriteVarInt(int value)
        {
            while ((value & 128) != 0)
            {
                _buffer.Add((byte) ((value & 127) | 128));
                value = (int) (uint) value >> 7;
            }

            _buffer.Add((byte) value);
        }

        private void WriteShort(short value)
        {
            _buffer.AddRange(BitConverter.GetBytes(value));
        }

        private void WriteString(string data)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(data);
            WriteVarInt(buffer.Length);
            _buffer.AddRange(buffer);
        }

        private void Write(byte b)
        {
            _stream.WriteByte(b);
        }

        private void Flush(int id = -1)
        {
            byte[] buffer = _buffer.ToArray();
            _buffer.Clear();

            var add = 0;
            var packetData = new[] {(byte) 0x00};
            if (id >= 0)
            {
                WriteVarInt(id);
                packetData = _buffer.ToArray();
                add = packetData.Length;
                _buffer.Clear();
            }

            WriteVarInt(buffer.Length + add);
            byte[] bufferLength = _buffer.ToArray();
            _buffer.Clear();

            _stream.Write(bufferLength, 0, bufferLength.Length);
            _stream.Write(packetData, 0, packetData.Length);
            _stream.Write(buffer, 0, buffer.Length);
        }
    }
}