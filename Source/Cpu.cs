using System;
using System.Collections.Generic;
using System.IO;

namespace Faux86
{
	internal class Cpu
	{
		private readonly Memory memory;
		private readonly List<Register> registers;
		private readonly List<Register> segmentRegisters; 
		private bool Cf, Pf, Af, Zf, Sf, Tf, If, Df, Of;
		private long ip;

		private enum RegisterCode : byte
		{
			Ax, Cx, Dx, Bx, Sp, Bp, Si, Di
		}

		private enum SegmentRegisterCode : byte
		{
			Es, Cs, Ss, Ds
		}

		public Cpu()
		{
			// 0x10FFF0 is the largest addressable seg:off address
			memory = new Memory(Memory.MaxAddressableMemory);
			registers = new List<Register>
			{
				new Register("AX"),
				new Register("CX"),
				new Register("DX"),
				new Register("BX"),
				new Register("SP"),
				new Register("BP"),
				new Register("SI"),
				new Register("DI")
			};

			segmentRegisters = new List<Register>
			{
				new Register("ES"),
				new Register("CS"),
				new Register("SS"),
				new Register("DS")
			};
		}

		public void Initialize(string biosFilename)
		{
			// Initialize initial register state
			segmentRegisters[(int)SegmentRegisterCode.Cs].Value = 0xF000;			// BIOS is loaded at F000:0100
			ip = 0x0100;
			Tf = false;

			// Set DL to 0x00 for floppies (for now)
			registers[(int)RegisterCode.Dx].Low = 0x00;

			// Load BIOS at F000:0100
			LoadBios(biosFilename, 0xF000, 0x0100);
		}

		public void Run()
		{
			/*
			 * REX Table and a /lot/ of other information
			 * http://brokenthorn.com/Resources/OSDevX86.html
			 * 
			 * Instructions
			 * http://x86.renejeschke.de/
			 */

			for (;;)
			{
				// [Operation][Mod R/M][Displacement][Immediate]
				// 0 0 0 0 0 0 d w
				ushort csValue = segmentRegisters[(int)SegmentRegisterCode.Cs].Value;
				byte instruction = memory.GetByte(csValue, ip);
				bool wFlag = (instruction & 0x01) == 0x01;
				bool dFlag = (instruction & 0x02) == 0x02;
				byte iReg = (byte)(instruction & 0x07);					// Instruction encoded register

				// Prefetch the rest of the instruction which may or may not be there
				byte data0 = memory.GetByte(csValue, ip + 1);
				byte data1 = memory.GetByte(csValue, ip + 2);
				byte data2 = memory.GetByte(csValue, ip + 3);

				/*
				 *	ModRM.mod	00: [Memory]
				 *				01: [Memory+DISP8]
				 *				10: [Memory+DISP16]
				 *				11: Register
				 *	ModRM.reg	Register code
				 *	ModRM.rm	If ModRM.mod = 11: register code
				 *				000: [BX+SI]
				 *				001: [BX+DI]
				 *				010: [BP+SI]
				 *				011: [BP+DI]
				 *				100: [SI]
				 *				101: [DI]
				 *				110: [BP] or [DISP16] when ModRM.mod=0
				 *				111: [BX]
				 */

				byte mod = (byte)((data0 & 0xC0) >> 6);
				byte reg = (byte)((data0 & 0x38) >> 3);
				byte rm = (byte)(data0 & 0x07);

				// Evaluate the address rm is referencing
				long rmAddr = 0;
				if (mod != 3)
				{
					switch (rm)
					{
						case 0x00:
							rmAddr = registers[(int)RegisterCode.Bx].Value + registers[(int)RegisterCode.Si].Value;
							break;

						case 0x01:
							rmAddr = registers[(int)RegisterCode.Bx].Value + registers[(int)RegisterCode.Di].Value;
							break;

						case 0x02:
							rmAddr = registers[(int)RegisterCode.Bp].Value + registers[(int)RegisterCode.Si].Value;
							break;

						case 0x03:
							rmAddr = registers[(int)RegisterCode.Bp].Value + registers[(int)RegisterCode.Di].Value;
							break;

						case 0x04:
							rmAddr = registers[(int)RegisterCode.Si].Value;
							break;

						case 0x05:
							rmAddr = registers[(int)RegisterCode.Di].Value;
							break;

						case 0x06:
							rmAddr = mod == 0 ? (ushort)((data2 << 8) | data1) : registers[(int)RegisterCode.Bp].Value;
							break;

						case 0x07:
							rmAddr = registers[(int)RegisterCode.Bx].Value;
							break;
					}
				}

				// HLT instruction used to break from execution for now
				if (instruction == 0xF4)
					break;

				// Execute instruction
				switch(instruction & 0xF0)
				{
					case 0xB0: // MOV reg, imm
						// w flag is displaced by the reg
						wFlag = (instruction & 0x08) == 0x08; // Word?
						if (wFlag)
							registers[iReg].Value = (ushort)((data1 << 8) | data0);
						else if (iReg < 4)
							registers[iReg].Low = data0;
						else
							registers[iReg - 4].High = data0;

						ip += 1 + (wFlag ? 2 : 1);
						break;

					case 0xA0: // MOV acc, [mem] | MOV [mem], acc
						ushort address = (ushort)((data1 << 8) | data0);

						// There is NO dFlag for this instruction, but it helps keep things clean
						// TODO Nested switch to check bit instead?
						if (dFlag)
						{
							// MOV [mem], acc
							if (wFlag)
								memory.SetWord(registers[(int)RegisterCode.Ax].Value, address);
							else
								memory.SetByte(registers[(int)RegisterCode.Ax].Low, address);
						}
						else
						{
							// MOV acc, [mem]
							if (wFlag)
								registers[(int)RegisterCode.Ax].Value = memory.GetWord(address);
							else
								registers[(int)RegisterCode.Ax].Low = memory.GetByte(address);
						}

						ip += 3;
						break;

					case 0x80: // MOV reg/mem, reg | MOV reg, reg/mem
						switch(instruction & 0x0F)
						{
							case 0x0C: // mov reg, sreg
								switch(mod)
								{
									case 0x00: // [Memory]
										throw new NotImplementedException();

									case 0x01: // [Memory + DISP8]
										throw new NotImplementedException();

									case 0x02: // [Memory + DISP16]
										throw new NotImplementedException();

									case 0x03: // Register
										registers[rm].Value = segmentRegisters[reg].Value;
										break;
								}
								break;

							case 0x0E: // mov sreg, reg
								switch(mod)
								{
									case 0x00: // [Memory]
										throw new NotImplementedException();

									case 0x01: // [Memory + DISP8]
										throw new NotImplementedException();

									case 0x02: // [Memory + DISP16]
										throw new NotImplementedException();

									case 0x03: // Register
										segmentRegisters[reg].Value = registers[rm].Value;
										break;
								}
								
								break;

							default:
								switch(mod)
								{
									case 0x00: // [Memory]
										if (dFlag)
										{
											if (wFlag)
												registers[reg].Value = memory.GetWord(rmAddr);
											else if (reg < 4)
												registers[reg].Low = memory.GetByte(rmAddr);
											else
												registers[reg - 4].High = memory.GetByte(rmAddr);
										}
										else
										{
											if (wFlag)
												memory.SetWord(registers[reg].Value, rmAddr);
											else if (reg < 4)
												memory.SetByte(registers[reg].Low, rmAddr);
											else
												memory.SetByte(registers[reg - 4].High, rmAddr);
										}
										break;

									case 0x01: // [Memory + DISP8]
										if (dFlag)
										{
											if (wFlag)
												registers[reg].Value = memory.GetWord(rmAddr + data1);
											else if (reg < 4)
												registers[reg].Low = memory.GetByte(rmAddr + data1);
											else
												registers[reg - 4].High = memory.GetByte(rmAddr + data1);
										}
										else
										{
											if (wFlag)
												memory.SetWord(registers[reg].Value, rmAddr + data1);
											else if (reg < 4)
												memory.SetByte(registers[reg].Low, rmAddr + data1);
											else
												memory.SetByte(registers[reg - 4].High, rmAddr + data1);
										}
										break;

									case 0x02: // [Memory + DISP16]
										if (dFlag)
										{
											if (wFlag)
												registers[reg].Value = memory.GetWord(rmAddr + (ushort)((data2 << 8) | data1));
											else if (reg < 4)
												registers[reg].Low = memory.GetByte(rmAddr + (ushort)((data2 << 8) | data1));
											else
												registers[reg - 4].High = memory.GetByte(rmAddr + (ushort)((data2 << 8) | data1));
										}
										else
										{
											if (wFlag)
												memory.SetWord(registers[reg].Value, rmAddr + (ushort)((data2 << 8) | data1));
											else if (reg < 4)
												memory.SetByte(registers[reg].Low, rmAddr + (ushort)((data2 << 8) | data1));
											else
												memory.SetByte(registers[reg - 4].High, rmAddr + (ushort)((data2 << 8) | data1));
										}
										break;

									case 0x03: // Register
										if (dFlag)
										{
											if (wFlag)
												registers[reg].Value = registers[rm].Value;
											else if (reg < 4)
												registers[reg].Low = rm < 4 ? registers[rm].Low : registers[rm - 4].High;
											else
												registers[reg - 4].High = rm < 4 ? registers[rm].Low : registers[rm - 4].High;
										}
										else
										{
											if (wFlag)
												registers[rm].Value = registers[reg].Value;
											else if (rm < 4)
												registers[rm].Low = reg < 4 ? registers[reg].Low : registers[reg - 4].High;
											else
												registers[rm - 4].High = reg < 4 ? registers[reg].Low : registers[reg - 4].High;
										}
										break;
								}
								break;
						}

						ip += 2 + (mod == 0 && rm == 0x06 ? 2 : mod == 0x02 ? 2 : mod == 0x01 ? 1 : 0);
						break;

				}
			}
		}

		public void DumpMachineState()
		{
			Console.WriteLine(" --- Registers ---");
			Console.WriteLine("{0}\t{1}\t{2}\t{3}", registers[0], registers[1], registers[2], registers[3]);
			Console.WriteLine("{0}\t{1}\t{2}\t{3}", registers[4], registers[5], registers[6], registers[7]);
			Console.WriteLine("{0}\t{1}\t{2}\t{3}", segmentRegisters[0], segmentRegisters[1], segmentRegisters[2],
													segmentRegisters[3]);

			Console.WriteLine("word [0x0500] = {0:X4}", memory.GetWord(0x0500));

		}

		private void LoadBios(string filename, long seg, long off)
		{
			using (BinaryReader reader = new BinaryReader(new FileStream(filename, FileMode.Open)))
			{
				while(reader.BaseStream.Position < reader.BaseStream.Length)
					memory.SetByte(reader.ReadByte(), seg, off++);
			}
		}
	}
}
