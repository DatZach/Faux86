﻿using System;

namespace Faux86
{
	internal class Register
	{
		private readonly string name;
		private ushort word;

		public ushort Value
		{
			get
			{
				return word;
			}

			set
			{
				word = value;
			}
		}

		public byte High
		{
			get
			{
				return (byte)((word & 0xFF00) >> 8);
			}

			set
			{
				word = (ushort)((word & 0x00FF) | (value << 8));
			}
		}

		public byte Low
		{
			get
			{
				return (byte)(word & 0x00FF);
			}

			set
			{
				word = (ushort)((word & 0xFF00) | value);
			}
		}

		public Register(string name)
		{
			this.name = name;
			word = 0x0000;
		}

		public override string ToString()
		{
			return String.Format("{0}={1:X4}", name.ToUpper(), Value);
		}
	}
}
