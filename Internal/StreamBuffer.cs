using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NDG
{
    public class StreamBuffer
    {
        private const int DefaultInitialSize = 0;
        private int pos;
        private int len;
        private byte[] buf;

        public StreamBuffer(int size = 0) => this.buf = new byte[size];

        public StreamBuffer(byte[] buf)
        {
            this.buf = buf;
            this.len = buf.Length;
        }

        public byte[] ToArray()
        {
            byte[] numArray = new byte[this.len];
            Buffer.BlockCopy((Array)this.buf, 0, (Array)numArray, 0, this.len);
            return numArray;
        }

        public byte[] ToArrayFromPos()
        {
            int count = this.len - this.pos;
            if (count <= 0)
                return new byte[0];
            byte[] numArray = new byte[count];
            Buffer.BlockCopy((Array)this.buf, this.pos, (Array)numArray, 0, count);
            return numArray;
        }

        /// <summary>
        /// Position과 Length 사이의 바이트가 버퍼의 시작 부분으로 복사됩니다.
        /// 길이가 Position만큼 감소합니다. Position이 0으로 설정됩니다.
        /// </summary>
        public void Compact()
        {
            long num = (long)(this.Length - this.Position);
            if (num > 0L)
                Buffer.BlockCopy((Array)this.buf, this.Position, (Array)this.buf, 0, (int)num);
            this.Position = 0;
            this.SetLength(num);
        }


        public byte[] GetBuffer() => this.buf;



        public byte[] GetBufferAndAdvance(int length, out int offset)
        {
            offset = this.Position;
            this.Position += length;
            return this.buf;
        }

        public bool CanRead => true;

        public bool CanSeek => true;

        public bool CanWrite => true;

        public int Length => this.len;

        public int Position
        {
            get => this.pos;
            set
            {
                this.pos = value;
                if (this.len >= this.pos)
                    return;
                this.len = this.pos;
                this.CheckSize(this.len);
            }
        }

        public long Seek(long offset, SeekOrigin origin)
        {
            int num;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    num = (int)offset;
                    break;
                case SeekOrigin.Current:
                    num = this.pos + (int)offset;
                    break;
                case SeekOrigin.End:
                    num = this.len + (int)offset;
                    break;
                default:
                    throw new ArgumentException("Invalid seek origin");
            }
            if (num < 0)
                throw new ArgumentException("Seek before begin");
            this.pos = num <= this.len ? num : throw new ArgumentException("Seek after end");
            return (long)this.pos;
        }

        /// <summary>
        /// Stream 길이를 설정합니다.
        /// 현재 위치가 지정된 값보다 크면 해당 값으로 설정됩니다.
        /// </summary>
        public void SetLength(long value)
        {
            this.len = (int)value;
            this.CheckSize(this.len);
            if (this.pos <= this.len)
                return;
            this.pos = this.len;
        }

        /// <summary>
        /// Buffer가 최소한 필요 크기를 충족하는지 확인합니다.
        /// </summary>
        public void SetCapacityMinimum(int neededSize) => this.CheckSize(neededSize);

        public int Read(byte[] buffer, int offset, int count)
        {
            int num = this.len - this.pos;
            if (num <= 0)
                return 0;
            if (count > num)
                count = num;
            Buffer.BlockCopy((Array)this.buf, this.pos, (Array)buffer, offset, count);
            this.pos += count;
            return count;
        }

        public void Write(byte[] buffer, int srcOffset, int count)
        {
            int size = this.pos + count;
            this.CheckSize(size);
            if (size > this.len)
                this.len = size;
            Buffer.BlockCopy((Array)buffer, srcOffset, (Array)this.buf, this.pos, count);
            this.pos = size;
        }

        public byte ReadByte() => this.pos < this.len ? this.buf[this.pos++] : throw new EndOfStreamException("SteamBuffer.ReadByte() failed. pos:" + this.pos.ToString() + "len:" + this.len.ToString());


        public void WriteByte(byte value)
        {
            if (this.pos >= this.len)
            {
                this.len = this.pos + 1;
                this.CheckSize(this.len);
            }
            this.buf[this.pos++] = value;
        }

        public void WriteBytes(byte v0, byte v1)
        {
            int num = this.pos + 2;
            if (this.len < num)
            {
                this.len = num;
                this.CheckSize(this.len);
            }
            this.buf[this.pos++] = v0;
            this.buf[this.pos++] = v1;
        }

        public void WriteBytes(byte v0, byte v1, byte v2)
        {
            int num = this.pos + 3;
            if (this.len < num)
            {
                this.len = num;
                this.CheckSize(this.len);
            }
            this.buf[this.pos++] = v0;
            this.buf[this.pos++] = v1;
            this.buf[this.pos++] = v2;
        }

        public void WriteBytes(byte v0, byte v1, byte v2, byte v3)
        {
            int num = this.pos + 4;
            if (this.len < num)
            {
                this.len = num;
                this.CheckSize(this.len);
            }
            this.buf[this.pos++] = v0;
            this.buf[this.pos++] = v1;
            this.buf[this.pos++] = v2;
            this.buf[this.pos++] = v3;
        }

        public void WriteBytes(
          byte v0,
          byte v1,
          byte v2,
          byte v3,
          byte v4,
          byte v5,
          byte v6,
          byte v7)
        {
            int num = this.pos + 8;
            if (this.len < num)
            {
                this.len = num;
                this.CheckSize(this.len);
            }
            this.buf[this.pos++] = v0;
            this.buf[this.pos++] = v1;
            this.buf[this.pos++] = v2;
            this.buf[this.pos++] = v3;
            this.buf[this.pos++] = v4;
            this.buf[this.pos++] = v5;
            this.buf[this.pos++] = v6;
            this.buf[this.pos++] = v7;
        }

        private bool CheckSize(int size)
        {
            if (size <= this.buf.Length)
                return false;
            int length = this.buf.Length;
            if (length == 0)
                length = 1;
            while (size > length)
                length *= 2;
            byte[] numArray = new byte[length];
            Buffer.BlockCopy((Array)this.buf, 0, (Array)numArray, 0, this.buf.Length);
            this.buf = numArray;
            return true;
        }
    }
}
