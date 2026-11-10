using System;
using System.Collections.Generic;
using TaggJack.Models;

namespace TaggJack.Services;

public static class Gs1SerialPlanner
{
	public static int CartonsOnPallet(int palletIndex, int totalCartons, int cartonsPerPallet)
	{
		int num = palletIndex * cartonsPerPallet;
		if (num >= totalCartons)
		{
			return 0;
		}
		return Math.Min(cartonsPerPallet, totalCartons - num);
	}

	public static int TotalPallets(int totalCartons, int cartonsPerPallet)
	{
		if (totalCartons <= 0 || cartonsPerPallet <= 0)
		{
			return 0;
		}
		return (int)Math.Ceiling((double)totalCartons / (double)cartonsPerPallet);
	}

	public static List<Gs1PackChunkPlan> PlanChunks(int totalCartons, int cartonsPerPallet, long palletsPerPackConfig)
	{
		List<Gs1PackChunkPlan> list = new List<Gs1PackChunkPlan>();
		int num = TotalPallets(totalCartons, cartonsPerPallet);
		if (num == 0)
		{
			return list;
		}
		long val = ((palletsPerPackConfig > 0) ? palletsPerPackConfig : num);
		int num2;
		for (int i = 0; i < num; i += num2)
		{
			num2 = (int)Math.Min(val, num - i);
			int num3 = 0;
			for (int j = i; j < i + num2; j++)
			{
				num3 += CartonsOnPallet(j, totalCartons, cartonsPerPallet);
			}
			list.Add(new Gs1PackChunkPlan
			{
				PalletOffset = i,
				PalletsInChunk = num2,
				TradeItemQty = num3,
				ShipperQty = cartonsPerPallet
			});
		}
		return list;
	}

	public static (int level1, int level2, int level3) CalculateSerialCounts(int quantity, int level1, int level2, int level3)
	{
		if (level1 <= 0 || level2 <= 0 || level3 <= 0)
		{
			throw new ArgumentException("Level values must be greater than zero.");
		}
		if (quantity % level1 != 0)
		{
			throw new ArgumentException($"Quantity {quantity:N0} is not evenly divisible by Level 1 ({level1}).");
		}
		int num = quantity / level1;
		if (num % level2 != 0)
		{
			throw new ArgumentException($"Level 1 serial count {num:N0} is not evenly divisible by Level 2 ({level2}).");
		}
		int num2 = num / level2;
		if (num2 % level3 != 0)
		{
			throw new ArgumentException($"Level 2 serial count {num2:N0} is not evenly divisible by Level 3 ({level3}).");
		}
		int item = num2 / level3;
		return (level1: num, level2: num2, level3: item);
	}
}
