using System;
using System.Collections.Generic;
using System.IO;

namespace Faux86
{
	internal class Cpu
	{
		public readonly Memory Memory;
		public readonly List<Register> Registers;
		public readonly List<Register> SegmentRegisters;
		public bool Cf, Pf, Af, Zf, Sf, Tf, If, Df, Of;
		public long Ip;

		public enum RegisterCode : byte
		{
			Ax, Cx, Dx, Bx, Sp, Bp, Si, Di
		}

		public enum SegmentRegisterCode : byte
		{
			Es, Cs, Ss, Ds
		}

		public ushort CodeSegment
		{
			get
			{
				// I'm too lazy to keep typing this out
				return SegmentRegisters[(int)SegmentRegisterCode.Cs].Value;
			}
		}

		public Cpu()
		{
			// 0x10FFF0 is the largest addressable seg:off address
			Memory = new Memory(Memory.MaxAddressableMemory);
			Registers = new List<Register>
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

			SegmentRegisters = new List<Register>
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
			SegmentRegisters[(int)SegmentRegisterCode.Cs].Value = 0xF000;			// BIOS is loaded at F000:0100
			Ip = 0x0100;
			Tf = false;

			// Set DL to 0x00 for floppies (for now)
			Registers[(int)RegisterCode.Dx].Low = 0x00;

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
				Instruction instruction = new Instruction(this);
				Console.WriteLine(instruction);

				switch(instruction.Op)
				{
					case Instruction.Opcode.Mov:
						switch(instruction.Operand1.Type)
						{
							case Instruction.Operand.OperandType.Register:
								switch(instruction.Operand2.Type)
								{
									case Instruction.Operand.OperandType.Register:
										instruction.Operand1.Register.Value = instruction.Operand2.Register.Value;
										break;

									case Instruction.Operand.OperandType.Memory:
										instruction.Operand1.Register.Value = Memory.GetWord(instruction.Operand2.Address);
										break;

									case Instruction.Operand.OperandType.Data:
										instruction.Operand1.Register.Value = instruction.Operand2.Data;
										break;
								}
								break;

							case Instruction.Operand.OperandType.Memory:
								switch(instruction.Operand2.Type)
								{
									case Instruction.Operand.OperandType.Register:
										Memory.SetWord(instruction.Operand2.Register.Value, instruction.Operand1.Address);
										break;

									case Instruction.Operand.OperandType.Memory:
										throw new InvalidOperationException();

									case Instruction.Operand.OperandType.Data:
										Memory.SetWord(instruction.Operand2.Data, instruction.Operand1.Address);
										break;
								}
								break;

							case Instruction.Operand.OperandType.Data:
								throw new InvalidOperationException();
						}
						break;

					case Instruction.Opcode.Hlt:
						return;
				}

				Ip += instruction.Size;

				// Execute instruction
				/*switch(instruction & 0xF0)
				{
					case 0xB0: // MOV reg, imm
						// w flag is displaced by the reg
						wFlag = (instruction & 0x08) == 0x08; // Word?
						if (wFlag)
							Registers[iReg].Value = (ushort)((data1 << 8) | data0);
						else if (iReg < 4)
							Registers[iReg].Low = data0;
						else
							Registers[iReg - 4].High = data0;

						Ip += 1 + (wFlag ? 2 : 1);
						break;

					case 0xA0: // MOV acc, [mem] | MOV [mem], acc
						ushort address = (ushort)((data1 << 8) | data0);

						// There is NO dFlag for this instruction, but it helps keep things clean
						// TODO Nested switch to check bit instead?
						if (dFlag)
						{
							// MOV [mem], acc
							if (wFlag)
								Memory.SetWord(Registers[(int)RegisterCode.Ax].Value, address);
							else
								Memory.SetByte(Registers[(int)RegisterCode.Ax].Low, address);
						}
						else
						{
							// MOV acc, [mem]
							if (wFlag)
								Registers[(int)RegisterCode.Ax].Value = Memory.GetWord(address);
							else
								Registers[(int)RegisterCode.Ax].Low = Memory.GetByte(address);
						}

						Ip += 3;
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
										Registers[rm].Value = SegmentRegisters[reg].Value;
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
										SegmentRegisters[reg].Value = Registers[rm].Value;
										break;
								}
								
								break;

							default:
								Register toRegister = reg < 4 || wFlag ? Registers[reg] : Registers[reg - 4];
								Register fromRegister = rm < 4 || wFlag ? Registers[rm] : Registers[rm - 4];

								switch(mod)
								{
									case 0x00: // [Memory]
										if (dFlag)
										{
											if (wFlag)
												Registers[reg].Value = Memory.GetWord(rmAddr);
											else if (reg < 4)
												Registers[reg].Low = Memory.GetByte(rmAddr);
											else
												Registers[reg - 4].High = Memory.GetByte(rmAddr);
										}
										else
										{
											if (wFlag)
												Memory.SetWord(Registers[reg].Value, rmAddr);
											else if (reg < 4)
												Memory.SetByte(Registers[reg].Low, rmAddr);
											else
												Memory.SetByte(Registers[reg - 4].High, rmAddr);
										}
										break;

									case 0x01: // [Memory + DISP8]
										if (dFlag)
										{
											if (wFlag)
												Registers[reg].Value = Memory.GetWord(rmAddr + data1);
											else if (reg < 4)
												Registers[reg].Low = Memory.GetByte(rmAddr + data1);
											else
												Registers[reg - 4].High = Memory.GetByte(rmAddr + data1);
										}
										else
										{
											if (wFlag)
												Memory.SetWord(Registers[reg].Value, rmAddr + data1);
											else if (reg < 4)
												Memory.SetByte(Registers[reg].Low, rmAddr + data1);
											else
												Memory.SetByte(Registers[reg - 4].High, rmAddr + data1);
										}
										break;

									case 0x02: // [Memory + DISP16]
										if (dFlag)
										{
											if (wFlag)
												Registers[reg].Value = Memory.GetWord(rmAddr + (ushort)((data2 << 8) | data1));
											else if (reg < 4)
												Registers[reg].Low = Memory.GetByte(rmAddr + (ushort)((data2 << 8) | data1));
											else
												Registers[reg - 4].High = Memory.GetByte(rmAddr + (ushort)((data2 << 8) | data1));
										}
										else
										{
											if (wFlag)
												Memory.SetWord(Registers[reg].Value, rmAddr + (ushort)((data2 << 8) | data1));
											else if (reg < 4)
												Memory.SetByte(Registers[reg].Low, rmAddr + (ushort)((data2 << 8) | data1));
											else
												Memory.SetByte(Registers[reg - 4].High, rmAddr + (ushort)((data2 << 8) | data1));
										}
										break;

									case 0x03: // Register
										if (dFlag)
										{
											if (wFlag)
												Registers[reg].Value = Registers[rm].Value;
											else if (reg < 4)
												Registers[reg].Low = rm < 4 ? Registers[rm].Low : Registers[rm - 4].High;
											else
												Registers[reg - 4].High = rm < 4 ? Registers[rm].Low : Registers[rm - 4].High;
										}
										else
										{
											if (wFlag)
												Registers[rm].Value = Registers[reg].Value;
											else if (rm < 4)
												Registers[rm].Low = reg < 4 ? Registers[reg].Low : Registers[reg - 4].High;
											else
												Registers[rm - 4].High = reg < 4 ? Registers[reg].Low : Registers[reg - 4].High;
										}
										break;
								}
								break;
						}

						Ip += 2 + (mod == 0 && rm == 0x06 ? 2 : mod == 0x02 ? 2 : mod == 0x01 ? 1 : 0);
						break;

				}*/
			}
		}

		public void DumpMachineState()
		{
			Console.WriteLine();
			Console.WriteLine("========== Registers ==========");
			Console.WriteLine("{0}\t{1}\t{2}\t{3}", Registers[0], Registers[1], Registers[2], Registers[3]);
			Console.WriteLine("{0}\t{1}\t{2}\t{3}", Registers[4], Registers[5], Registers[6], Registers[7]);
			Console.WriteLine("{0}\t{1}\t{2}\t{3}", SegmentRegisters[0], SegmentRegisters[1], SegmentRegisters[2],
													SegmentRegisters[3]);

			Console.WriteLine();
			Console.WriteLine("word [0x0500] = {0:X4}", Memory.GetWord(0x0500));

		}

		private void LoadBios(string filename, long seg, long off)
		{
			using (BinaryReader reader = new BinaryReader(new FileStream(filename, FileMode.Open)))
			{
				while(reader.BaseStream.Position < reader.BaseStream.Length)
					Memory.SetByte(reader.ReadByte(), seg, off++);
			}
		}
	}
}
