using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NDG
{
    /// <summary>
    /// 풀링하여 재사용해야하는 메모리 조각입니다.
    /// </summary>
    public class ByteArraySlice : IDisposable
    {
        public byte[] Buffer;
        public int Offset;
        public int Count;
        private ByteArraySlicePool returnPool;
        private readonly int stackIndex;

        internal ByteArraySlice(ByteArraySlicePool returnPool, int stackIndex)
        {
            this.Buffer = stackIndex == 0 ? (byte[])null : new byte[1 << stackIndex];
            this.returnPool = returnPool;
            this.stackIndex = stackIndex;
        }


        public ByteArraySlice(byte[] buffer, int offset = 0, int count = 0)
        {
            this.Buffer = buffer;
            this.Count = count;
            this.Offset = offset;
            this.returnPool = (ByteArraySlicePool)null;
            this.stackIndex = -1;
        }

        public ByteArraySlice()
        {
            this.returnPool = (ByteArraySlicePool)null;
            this.stackIndex = -1;
        }

        public void Dispose() => this.Release();

        /// <summary>
        /// 이 항목이 ByteArraySlicePool에서 가져온 것이라면 이함수는 이를 반환합니다.
        /// </summary>
        /// <returns>풀링된 항목이고 성공적으로 반환된 경우 참입니다.</returns>
        public bool Release()
        {
            if (this.stackIndex < 0)
                return false;
            if (this.stackIndex == 0)
                this.Buffer = (byte[])null;
            this.Count = 0;
            this.Offset = 0;
            this.returnPool.poolTiers[this.stackIndex].Push(this);
            return true;
        }

        public void Reset()
        {
            this.Count = 0;
            this.Offset = 0;
        }
    }
}
