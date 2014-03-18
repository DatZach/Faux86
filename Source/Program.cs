using System;

namespace Faux86
{
	public class Program
	{
		public static void Main(string[] args)
		{
			Cpu cpu = new Cpu();
			cpu.Initialize("bios");
			cpu.Run();
			cpu.DumpMachineState();
			Console.ReadKey();
		}
	}
}
