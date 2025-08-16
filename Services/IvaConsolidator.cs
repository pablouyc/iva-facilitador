using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using IvaFacilitador.Models;

namespace IvaFacilitador.Services
{
    public static class IvaConsolidator
    {
        public static List<TarifaUsada> ConsolidarTarifasIVA(
            IEnumerable<dynamic> transacciones,
            IEnumerable<JsonElement> taxCodes,
            IEnumerable<JsonElement> taxRates)
        {
            var rateMap = new Dictionary<string, JsonElement>();
            foreach (var rate in taxRates)
            {
                if (rate.TryGetProperty("Id", out var id) && id.ValueKind == JsonValueKind.String)
                    rateMap[id.GetString()!] = rate;
            }

            var codeToRates = new Dictionary<string, List<string>>();
            foreach (var code in taxCodes)
            {
                if (code.TryGetProperty("Id", out var id) && id.ValueKind == JsonValueKind.String)
                {
                    var list = new List<string>();
                    if (code.TryGetProperty("SalesTaxRateList", out var rtl) &&
                        rtl.TryGetProperty("TaxRateDetail", out var details) &&
                        details.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var d in details.EnumerateArray())
                        {
                            if (d.TryGetProperty("TaxRateRef", out var rr) &&
                                rr.TryGetProperty("value", out var val) &&
                                val.ValueKind == JsonValueKind.String)
                            {
                                list.Add(val.GetString()!);
                            }
                        }
                    }
                    codeToRates[id.GetString()!] = list;
                }
            }

            var rateToCodes = new Dictionary<string, List<string>>();
            foreach (var kv in codeToRates)
            {
                foreach (var rateId in kv.Value)
                {
                    if (!rateToCodes.TryGetValue(rateId, out var list))
                    {
                        list = new List<string>();
                        rateToCodes[rateId] = list;
                    }
                    list.Add(kv.Key);
                }
            }

            var result = new Dictionary<(string CodeId, string RateId), TarifaUsada>();

            foreach (var tx in transacciones)
            {
                var dict = tx as IDictionary<string, object?> ?? new Dictionary<string, object?>();

                dict.TryGetValue("Entity", out var entObj);
                var sign = string.Equals(entObj as string, "CreditMemo", StringComparison.OrdinalIgnoreCase) ? -1 : 1;

                DateTime? fecha = null;
                if (dict.TryGetValue("TxnDate", out var dateObj) && dateObj is string dateStr && DateTime.TryParse(dateStr, out var d))
                    fecha = d;

                var lines = dict.TryGetValue("Line", out var lineObj) && lineObj is JsonElement lineEl ? lineEl : default;
                var taxDetail = dict.TryGetValue("TxnTaxDetail", out var taxObj) && taxObj is JsonElement taxEl ? taxEl : default;

                var lineBaseByCode = new Dictionary<string, decimal>();
                if (lines.ValueKind == JsonValueKind.Array)
                {
                    foreach (var line in lines.EnumerateArray())
                    {
                        if (line.TryGetProperty("TaxCodeRef", out var codeRef) &&
                            codeRef.TryGetProperty("value", out var codeVal) &&
                            codeVal.ValueKind == JsonValueKind.String &&
                            line.TryGetProperty("Amount", out var amtElem) &&
                            amtElem.ValueKind == JsonValueKind.Number)
                        {
                            var codeId = codeVal.GetString()!;
                            var amt = amtElem.GetDecimal() * sign;
                            lineBaseByCode[codeId] = lineBaseByCode.GetValueOrDefault(codeId) + amt;
                        }
                    }
                }

                var processed = new HashSet<(string, string)>();

                if (taxDetail.ValueKind == JsonValueKind.Object &&
                    taxDetail.TryGetProperty("TaxLine", out var taxLines) &&
                    taxLines.ValueKind == JsonValueKind.Array)
                {
                    foreach (var taxLine in taxLines.EnumerateArray())
                    {
                        if (!taxLine.TryGetProperty("TaxLineDetail", out var detail))
                            continue;
                        if (!detail.TryGetProperty("TaxRateRef", out var rateRef) ||
                            !rateRef.TryGetProperty("value", out var rateVal) ||
                            rateVal.ValueKind != JsonValueKind.String)
                            continue;
                        var rateId = rateVal.GetString()!;

                        var taxAmt = taxLine.TryGetProperty("Amount", out var taxAmtEl) && taxAmtEl.ValueKind == JsonValueKind.Number ? taxAmtEl.GetDecimal() * sign : 0m;
                        var baseAmt = detail.TryGetProperty("NetAmountTaxable", out var baseEl) && baseEl.ValueKind == JsonValueKind.Number ? baseEl.GetDecimal() * sign : 0m;

                        string? codeId = null;
                        if (rateToCodes.TryGetValue(rateId, out var candidates))
                        {
                            foreach (var c in candidates)
                            {
                                if (lineBaseByCode.ContainsKey(c))
                                {
                                    codeId = c;
                                    break;
                                }
                            }
                            codeId ??= candidates.FirstOrDefault();
                        }

                        if (codeId == null)
                            continue;

                        var key = (codeId, rateId);
                        processed.Add(key);

                        if (!result.TryGetValue(key, out var tu))
                        {
                            tu = new TarifaUsada
                            {
                                TaxCode = codeId,
                                TaxRate = rateId,
                                CountTransacciones = 0,
                                SumBase = 0,
                                SumImpuesto = 0,
                                PrimerUso = fecha,
                                UltimoUso = fecha
                            };
                            result[key] = tu;
                        }

                        tu.CountTransacciones += 1;
                        tu.SumBase += baseAmt;
                        tu.SumImpuesto += taxAmt;
                        if (fecha.HasValue)
                        {
                            if (!tu.PrimerUso.HasValue || fecha.Value < tu.PrimerUso.Value)
                                tu.PrimerUso = fecha;
                            if (!tu.UltimoUso.HasValue || fecha.Value > tu.UltimoUso.Value)
                                tu.UltimoUso = fecha;
                        }
                    }
                }

                foreach (var kv in lineBaseByCode)
                {
                    var codeId = kv.Key;
                    var baseAmt = kv.Value;
                    if (!codeToRates.TryGetValue(codeId, out var ratesForCode))
                        continue;

                    foreach (var rateId in ratesForCode)
                    {
                        if (processed.Contains((codeId, rateId)))
                            continue;
                        if (!rateMap.TryGetValue(rateId, out var rateEl))
                            continue;
                        var rateValue = rateEl.TryGetProperty("RateValue", out var rv) && rv.ValueKind == JsonValueKind.Number ? rv.GetDecimal() : -1m;
                        if (rateValue != 0m)
                            continue;

                        var key = (codeId, rateId);
                        if (!result.TryGetValue(key, out var tu))
                        {
                            tu = new TarifaUsada
                            {
                                TaxCode = codeId,
                                TaxRate = rateId,
                                CountTransacciones = 0,
                                SumBase = 0,
                                SumImpuesto = 0,
                                PrimerUso = fecha,
                                UltimoUso = fecha
                            };
                            result[key] = tu;
                        }

                        tu.CountTransacciones += 1;
                        tu.SumBase += baseAmt;
                        if (fecha.HasValue)
                        {
                            if (!tu.PrimerUso.HasValue || fecha.Value < tu.PrimerUso.Value)
                                tu.PrimerUso = fecha;
                            if (!tu.UltimoUso.HasValue || fecha.Value > tu.UltimoUso.Value)
                                tu.UltimoUso = fecha;
                        }
                    }
                }
            }

            return result.Values.ToList();
        }
    }
}
