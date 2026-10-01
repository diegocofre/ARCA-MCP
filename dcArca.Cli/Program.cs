/*
 * Copyright (c) 2025 Diego Cofré, DC Sistemas
 * www.diegocofre.com.ar
 *
 * Licensed under the Apache License, Version 2.0.
 */

using System.Text.Json;
using dcArca.Core.Models;
using dcArca.Core.Services;
using Microsoft.Extensions.Configuration;

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

var config = LoadConfig();
IdcWsfeClient wsfe = new dcWsfeClient(config);
IdcPadronClient padron = new dcPadronClient(config);
var jsonOptions = new JsonSerializerOptions { WriteIndented = true };

try
{
    object result = args[0] switch
    {
        "facturar" => await FacturarAsync(args, wsfe),
        "ultimo-autorizado" => await wsfe.FECompUltimoAutorizadoAsync((dcTipoComprobante)int.Parse(args[1])),
        "consultar" => await wsfe.FECompConsultarAsync(long.Parse(args[1]), (dcTipoComprobante)int.Parse(args[2])),
        "padron" => await padron.GetPersonaAsync(long.Parse(args[1])),
        "condiciones-iva" => await wsfe.GetCondicionesIVAReceptorAsync(int.Parse(args[1]), long.Parse(args[2]), (dcTipoComprobante)int.Parse(args[3])),
        _ => throw new ArgumentException($"Comando desconocido: {args[0]}")
    };

    Console.WriteLine(JsonSerializer.Serialize(result, jsonOptions));
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    PrintUsage();
    return 1;
}

static dcArcaConfig LoadConfig()
{
    var configuration = new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true)
        .AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"), optional: true)
        .AddEnvironmentVariables()
        .Build();

    var config = new dcArcaConfig();
    configuration.GetSection("dcArcaConfig").Bind(config);
    return config;
}

static async Task<dcFacturaResponse> FacturarAsync(string[] args, IdcWsfeClient wsfe)
{
    var json = args.Length > 1 && File.Exists(args[1])
        ? await File.ReadAllTextAsync(args[1])
        : await Console.In.ReadToEndAsync();

    var factura = JsonSerializer.Deserialize<dcFacturaRequest>(
        json,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new ArgumentException("JSON de factura inválido o vacío.");

    return await wsfe.FECAESolicitarAsync(factura);
}

static void PrintUsage()
{
    Console.Error.WriteLine(
        "dcArca.Cli <comando> [argumentos]\n" +
        "  facturar [archivo.json]\n" +
        "  ultimo-autorizado <tipoComprobante>\n" +
        "  consultar <numero> <tipoComprobante>\n" +
        "  padron <cuit>\n" +
        "  condiciones-iva <docTipo> <docNro> <tipoComprobante>");
}
