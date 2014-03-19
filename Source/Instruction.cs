using System;
using System.Collections.Generic;
using System.Text;

namespace Faux86
{
	internal class Instruction
	{
		private readonly InstructionInformationList instructionTable = new InstructionInformationList
		{
			// Register/Memory to/from Register
			{ Opcode.Mov, 0x88, 2, EncodingFlags.ModRm | EncodingFlags.Direction | EncodingFlags.Width },
			{ Opcode.Mov, 0x89, 2, EncodingFlags.ModRm | EncodingFlags.Direction | EncodingFlags.Width },
			{ Opcode.Mov, 0x8A, 2, EncodingFlags.ModRm | EncodingFlags.Direction | EncodingFlags.Width },
			{ Opcode.Mov, 0x8B, 2, EncodingFlags.ModRm | EncodingFlags.Direction | EncodingFlags.Width },

			// Register/Memory to Segment Register
			{ Opcode.Mov, 0x8C, 2, EncodingFlags.ModRm | EncodingFlags.ModRmSegReg },
			{ Opcode.Mov, 0x8E, 2, EncodingFlags.ModRm | EncodingFlags.ModRmSegReg },

			// Immediate to Register
			{ Opcode.Mov, 0xB0, 2, EncodingFlags.Width | EncodingFlags.Register | EncodingFlags.Data },
			{ Opcode.Mov, 0xB1, 2, EncodingFlags.Width | EncodingFlags.Register | EncodingFlags.Data },
			{ Opcode.Mov, 0xB2, 2, EncodingFlags.Width | EncodingFlags.Register | EncodingFlags.Data },
			{ Opcode.Mov, 0xB3, 2, EncodingFlags.Width | EncodingFlags.Register | EncodingFlags.Data },
			{ Opcode.Mov, 0xB4, 2, EncodingFlags.Width | EncodingFlags.Register | EncodingFlags.Data },
			{ Opcode.Mov, 0xB5, 2, EncodingFlags.Width | EncodingFlags.Register | EncodingFlags.Data },
			{ Opcode.Mov, 0xB6, 2, EncodingFlags.Width | EncodingFlags.Register | EncodingFlags.Data },
			{ Opcode.Mov, 0xB7, 2, EncodingFlags.Width | EncodingFlags.Register | EncodingFlags.Data },
			{ Opcode.Mov, 0xB8, 3, EncodingFlags.Width | EncodingFlags.Register | EncodingFlags.Data },
			{ Opcode.Mov, 0xB9, 3, EncodingFlags.Width | EncodingFlags.Register | EncodingFlags.Data },
			{ Opcode.Mov, 0xBA, 3, EncodingFlags.Width | EncodingFlags.Register | EncodingFlags.Data },
			{ Opcode.Mov, 0xBB, 3, EncodingFlags.Width | EncodingFlags.Register | EncodingFlags.Data },
			{ Opcode.Mov, 0xBC, 3, EncodingFlags.Width | EncodingFlags.Register | EncodingFlags.Data },
			{ Opcode.Mov, 0xBD, 3, EncodingFlags.Width | EncodingFlags.Register | EncodingFlags.Data },
			{ Opcode.Mov, 0xBE, 3, EncodingFlags.Width | EncodingFlags.Register | EncodingFlags.Data },
			{ Opcode.Mov, 0xBF, 3, EncodingFlags.Width | EncodingFlags.Register | EncodingFlags.Data },
			
			// Memory to Accumulator -- technically has a direction flag
			{ Opcode.Mov, 0xA0, 3, EncodingFlags.Width | EncodingFlags.Address | EncodingFlags.Direction },
			{ Opcode.Mov, 0xA1, 3, EncodingFlags.Width | EncodingFlags.Address | EncodingFlags.Direction },

			// Accumulator to Memory
			{ Opcode.Mov, 0xA2, 3, EncodingFlags.Width | EncodingFlags.Address | EncodingFlags.Direction },
			{ Opcode.Mov, 0xA3, 3, EncodingFlags.Width | EncodingFlags.Address | EncodingFlags.Direction },

			// Halt
			{ Opcode.Hlt, 0xF4, 2, 0 },
		};

		public Opcode Op
		{
			get
			{
				return info.Opcode;
			}
		}

		public int Size { get; private set; }

		public Operand Operand1 { get; private set; }
		public Operand Operand2 { get; private set; }

		private readonly InstructionInformation info;

		public Instruction(Cpu cpu)
		{
			// [Operation][Mod R/M][Displacement][Immediate]

			// Read instruction at the current CS:IP
			byte encoding = cpu.Memory.GetByte(cpu.CodeSegment, cpu.Ip);

			// Look it up in the instruction decode table
			info = instructionTable.Find(m => m.Encoding == encoding);
			if (info == null)
				throw new Exception("Cannot decode instruction!");

			// Resolve operands
			Operand1 = Operand2 = null;
			Size = 1;
			ResolveOperands(cpu);
		}

		private void ResolveOperands(Cpu cpu)
		{
			byte iReg = 0;
			bool wFlag;

			if (info.Flags.HasFlag(EncodingFlags.Register))
			{
				iReg = (byte)(info.Encoding & 0x07);
				wFlag = (info.Encoding & 0x08) == 0x08;
			}
			else
				wFlag = (info.Encoding & 0x01) == 0x01;
			
			bool dFlag = (info.Encoding & 0x02) == 0x02;

			// Prefetch the rest of the instruction which may or may not be there
			int dataIndex = 0;
			byte[] data =
			{
				cpu.Memory.GetByte(cpu.CodeSegment, cpu.Ip + 1),
				cpu.Memory.GetByte(cpu.CodeSegment, cpu.Ip + 2),
				cpu.Memory.GetByte(cpu.CodeSegment, cpu.Ip + 3)
			};

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

			if (info.Flags.HasFlag(EncodingFlags.ModRm))
			{
				byte mod = (byte)((data[dataIndex] & 0xC0) >> 6);
				byte reg = (byte)((data[dataIndex] & 0x38) >> 3);
				byte rm = (byte)(data[dataIndex] & 0x07);
				++dataIndex;

				// Evaluate the address rm is referencing
				long rmAddr = 0;
				switch(rm)
				{
					case 0x00:
						rmAddr = cpu.Registers[(int)Cpu.RegisterCode.Bx].Value +
						         cpu.Registers[(int)Cpu.RegisterCode.Si].Value;
						break;

					case 0x01:
						rmAddr = cpu.Registers[(int)Cpu.RegisterCode.Bx].Value +
						         cpu.Registers[(int)Cpu.RegisterCode.Di].Value;
						break;

					case 0x02:
						rmAddr = cpu.Registers[(int)Cpu.RegisterCode.Bp].Value +
						         cpu.Registers[(int)Cpu.RegisterCode.Si].Value;
						break;

					case 0x03:
						rmAddr = cpu.Registers[(int)Cpu.RegisterCode.Bp].Value +
						         cpu.Registers[(int)Cpu.RegisterCode.Di].Value;
						break;

					case 0x04:
						rmAddr = cpu.Registers[(int)Cpu.RegisterCode.Si].Value;
						break;

					case 0x05:
						rmAddr = cpu.Registers[(int)Cpu.RegisterCode.Di].Value;
						break;

					case 0x06:
						rmAddr = mod == 0
									? (ushort)((data[dataIndex + 1] << 8) | data[dataIndex])
									: cpu.Registers[(int)Cpu.RegisterCode.Bp].Value;
						break;

					case 0x07:
						rmAddr = cpu.Registers[(int)Cpu.RegisterCode.Bx].Value;
						break;
				}

				Operand modRmOperand = null;

				switch(mod)
				{
					case 0: // 00 [Memory]
						modRmOperand = new Operand(rmAddr);
						break;
					
					case 1: // 01 [Memory+DISP8]
						modRmOperand = new Operand(rmAddr + data[dataIndex]);
						break;
					
					case 2: // 10 [Memory+DISP16]
						modRmOperand = new Operand(rmAddr + (ushort)((data[dataIndex + 1] << 8) | data[dataIndex]));
						// TODO Should be incremented earlier?
						dataIndex += 2;
						break;

					case 3: // 11 Register
						modRmOperand = info.Flags.HasFlag(EncodingFlags.ModRmSegReg)
										? new Operand(cpu.SegmentRegisters[reg])
										: new Operand(cpu.Registers[reg]);
						break;
				}

				if (dFlag)
					Operand1 = modRmOperand;
				else
					Operand2 = modRmOperand;

				if (info.Flags.HasFlag(EncodingFlags.ModRmSegReg))
				{
					if (dFlag)
						Operand2 = new Operand(cpu.Registers[rm]);
					else
						Operand1 = new Operand(cpu.Registers[rm]);
				}
			}

			if (info.Flags.HasFlag(EncodingFlags.Register))
			{
				if (dFlag)
					Operand2 = new Operand(cpu.Registers[iReg]);
				else
					Operand1 = new Operand(cpu.Registers[iReg]);
			}
			
			if (info.Flags.HasFlag(EncodingFlags.Data) && info.Flags.HasFlag(EncodingFlags.Width))
			{
				// Technically all instructions encoding data should have a width flag, should be safe...
				ushort operandData = wFlag ? (ushort)((data[dataIndex + 1] << 8) | data[dataIndex]) : data[dataIndex];
				dataIndex += wFlag ? 2 : 1;

				if (dFlag)
					Operand1 = new Operand(operandData);
				else
					Operand2 = new Operand(operandData);
			}

			Size += dataIndex;
		}

		public override string ToString()
		{
			StringBuilder builder = new StringBuilder();
			builder.AppendFormat("{0}\t", Op.ToString().ToUpper());

			if (Operand1 != null)
				builder.Append(Operand1);

			if (Operand2 != null)
				builder.AppendFormat(", {0}", Operand2);

			return builder.ToString();
		}

		public class Operand
		{
			public OperandType Type { get; private set; }
			public Register Register { get; private set; }
			public long Address { get; private set; }
			public ushort Data { get; private set; }

			public Operand(Register register)
			{
				Type = OperandType.Register;
				Register = register;
				Address = 0;
				Data = 0;
			}

			public Operand(long address)
			{
				Type = OperandType.Memory;
				Register = null;
				Address = address;
				Data = 0;
			}

			public Operand(ushort data)
			{
				Type = OperandType.Data;
				Register = null;
				Address = 0;
				Data = data;
			}

			public enum OperandType
			{
				Register, Memory, Data
			}

			public override string ToString()
			{
				StringBuilder builder = new StringBuilder();

				switch (Type)
				{
					case OperandType.Memory:
						builder.AppendFormat("[0x{0:X4}]", Address);
						break;

					case OperandType.Register:
						builder.Append(Register.Name);
						break;

					case OperandType.Data:
						builder.AppendFormat("0x{0:X4}", Data);
						break;
				}

				return builder.ToString();
			}
		}

		/*
		 *	The order here doesn't matter, taken directly
		 *	from the Intel docs.
		 */
		public enum Opcode : byte
		{
			Mov,
			Hlt
		}

		private class InstructionInformation
		{
			public Opcode Opcode { get; private set; }
			public byte Encoding { get; private set; }
			public int Size { get; private set; }
			public EncodingFlags Flags { get; private set; }

			public InstructionInformation(Opcode opcode, byte encoding, int size, EncodingFlags flags)
			{
				Opcode = opcode;
				Encoding = encoding;
				Size = size;
				Flags = flags;
			}
		}

		[Flags]
		private enum EncodingFlags
		{
			Direction = 0x01,		// Direction flag in opcode
			Width = 0x02,			// Width flag in opcode
			Register = 0x04,		// Register in opcode
			ModRm = 0x08,			// Has a ModR/M
			ModRmSegReg = 0x10,		// ModR/M reference segment register
			Data = 0x20,			// Has a data payload (if w == 1 then it's 2 bytes)
			Address = 0x40,			// Has an address payload (always 2 bytes), can be DISP16 in some instructions
		}

		private class InstructionInformationList : List<InstructionInformation>
		{
			public void Add(Opcode opcode, byte encoding, int size, EncodingFlags flags)
			{
				Add(new InstructionInformation(opcode, encoding, size, flags));
			}
		}
	}
}
