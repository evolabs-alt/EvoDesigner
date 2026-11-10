using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using TaggJack.Models;

namespace TaggJack.Services;

public class Gs1SerialGenerationService
{
	private readonly Gs1ActivationClient _client = new Gs1ActivationClient();

	public async Task<Gs1GenerationResult> GenerateAsync(AppConfig config, ItemRecord item, int quantity, string batch, string production, string expiry, string fromGln, string toGln, string? orderNumber = null)
	{
		if (string.IsNullOrWhiteSpace(fromGln))
		{
			throw new InvalidOperationException("From GLN is required. Set it in GS1 Settings or on the Generate form.");
		}
		if (string.IsNullOrWhiteSpace(toGln))
		{
			throw new InvalidOperationException("To GLN is required. Set it in GS1 Settings or on the Generate form.");
		}
		(int level1, int level2, int level3) counts = Gs1SerialPlanner.CalculateSerialCounts(quantity, item.Level1, item.Level2, item.Level3);
		int totalCartons = counts.level1;
		int level = item.Level2;
		long palletsPerPackConfig = item.Level3;
		List<Gs1PackChunkPlan> list = Gs1SerialPlanner.PlanChunks(totalCartons, level, palletsPerPackConfig);
		if (list.Count == 0)
		{
			throw new InvalidOperationException("No GS1 pack chunks could be planned for this quantity.");
		}
		List<Gs1SerialRecord> level1Serials = new List<Gs1SerialRecord>();
		List<Gs1SerialRecord> level2Serials = new List<Gs1SerialRecord>();
		List<Gs1SerialRecord> level3Serials = new List<Gs1SerialRecord>();
		List<Gs1ActivationResponse> rawResponses = new List<Gs1ActivationResponse>();
		string? order = string.IsNullOrWhiteSpace(orderNumber) ? null : orderNumber.Trim();
		foreach (Gs1PackChunkPlan chunk in list)
		{
			Gs1ActivationRequest request = new Gs1ActivationRequest
			{
				Barcode = item.Gtin,
				Batch = batch,
				Production = production,
				Expiry = expiry,
				TradeItemQty = chunk.TradeItemQty.ToString(),
				ShipperQty = chunk.ShipperQty.ToString(),
				From = new Gs1PartyGln { Gln = fromGln.Trim() },
				To = new Gs1PartyGln { Gln = toGln.Trim() },
				OrderNumber = order
			};
			Gs1ActivationResponse gs1ActivationResponse = await _client.ActivateAsync(config.Gs1.ApiUrl, config.Gs1.BearerToken, request).ConfigureAwait(continueOnCapturedContext: false);
			ValidateResponse(gs1ActivationResponse, chunk);
			rawResponses.Add(gs1ActivationResponse);
			level3Serials.Add(new Gs1SerialRecord
			{
				Level = 3,
				Serial = gs1ActivationResponse.Sscc,
				ParentSerial = "",
				Sscc = gs1ActivationResponse.Sscc
			});
			foreach (Gs1ShipperResponse shipper in gs1ActivationResponse.Shippers)
			{
				level2Serials.Add(new Gs1SerialRecord
				{
					Level = 2,
					Serial = shipper.Serial,
					ParentSerial = gs1ActivationResponse.Sscc,
					Sscc = gs1ActivationResponse.Sscc
				});
				if (shipper.PrimarySerials == null)
				{
					continue;
				}
				foreach (string primarySerial in shipper.PrimarySerials)
				{
					level1Serials.Add(new Gs1SerialRecord
					{
						Level = 1,
						Serial = primarySerial,
						ParentSerial = shipper.Serial,
						Sscc = gs1ActivationResponse.Sscc
					});
				}
			}
		}
		if (level1Serials.Count != totalCartons)
		{
			throw new InvalidOperationException($"Expected {totalCartons} Level 1 serials but received {level1Serials.Count}.");
		}
		return new Gs1GenerationResult
		{
			Level1Count = counts.level1,
			Level2Count = counts.level2,
			Level3Count = counts.level3,
			Level1Serials = level1Serials,
			Level2Serials = level2Serials,
			Level3Serials = level3Serials,
			RawResponseJson = JsonSerializer.Serialize(rawResponses)
		};
	}

	private static void ValidateResponse(Gs1ActivationResponse response, Gs1PackChunkPlan chunk)
	{
		if (string.IsNullOrWhiteSpace(response.Sscc))
		{
			throw new InvalidOperationException("GS1 API did not return pack SSCC.");
		}
		if (response.Shippers == null || response.Shippers.Count == 0)
		{
			throw new InvalidOperationException("GS1 API did not return shippers.");
		}
		if (response.PrimaryCount.HasValue && response.PrimaryCount.Value != chunk.TradeItemQty)
		{
			throw new InvalidOperationException("GS1 primary_count mismatch.");
		}
		if (response.ShipperCount.HasValue && response.ShipperCount.Value != chunk.PalletsInChunk)
		{
			throw new InvalidOperationException($"GS1 shipper_count mismatch: expected {chunk.PalletsInChunk}, got {response.ShipperCount}.");
		}
	}
}
