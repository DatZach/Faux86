namespace Faux86
{
	internal class Memory
	{
		public const long MaxAddressableMemory = 0x10FFF0;

		public long Size
		{
			get
			{
				return memory.Length;
			}
		}

		private readonly byte[] memory;

		public byte this[long i]
		{
			get
			{
				return memory[i];
			}

			set
			{
				memory[i] = value;
			}
		}

		public Memory(long length)
		{
			memory = new byte[length];
		}

		public byte GetByte(long seg, long off)
		{
			long address = SegOffToLinear(seg, off);
			return memory[address];
		}

		public void SetByte(byte value, long seg, long off)
		{
			long address = SegOffToLinear(seg, off);
			memory[address] = value;
		}

		public byte GetByte(long address)
		{
			return memory[address];
		}

		public void SetByte(byte value, long address)
		{
			memory[address] = value;
		}

		public ushort GetWord(long seg, long off)
		{
			long address = SegOffToLinear(seg, off);
			return (ushort)((memory[address] << 8) | memory[address + 1]);
		}

		public void SetWord(ushort value, long seg, long off)
		{
			long address = SegOffToLinear(seg, off);
			memory[address + 1] = (byte)(value & 0x00FF);
			memory[address] = (byte)((value & 0xFF00) >> 8);
		}

		public ushort GetWord(long address)
		{
			return (ushort)((memory[address] << 8) | memory[address + 1]);
		}

		public void SetWord(ushort value, long address)
		{
			memory[address + 1] = (byte)(value & 0x00FF);
			memory[address] = (byte)((value & 0xFF00) >> 8);
		}

		public static long SegOffToLinear(long seg, long off)
		{
			return seg * 16 + off;
		}
	}
}
