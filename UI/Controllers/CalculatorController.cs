﻿using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;
using RestSharp;
using Serilog;
using SharedModels.Models;
using UI.Models;

namespace UI.Controllers;

public class CalculatorController : Controller
{
    private readonly IHttpClientFactory _clientFactory;

    public CalculatorController(IHttpClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }
    [HttpGet]
    public async Task<IActionResult> IndexAsync()
    {
        await LoadDataAsync();
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Calculate(string numberInput, string operation)
    {
        List<int> numbers;
        try
        {
            numbers = numberInput.Split(',').Select(int.Parse).ToList();
        }
        catch (FormatException)
        {
            ViewBag.Error = "Invalid number format.";
            return View("Index");
        }

        var client = _clientFactory.CreateClient("MyClient");
        var queryString = string.Join("&input=", numbers.Prepend(0));

        string result = "";
        string resp = "";
        if (operation == "Add")
        {
            // Testing an empty List (For logging)
            // var temp = new List<int>();
            result = await client.GetStringAsync($"http://adding-service/Add?{queryString}");
            if (result != null)
            {
                resp = await PushIntoDatabase(numbers, operation, result);
            }
        }
        else if (operation == "Sub")
        {
            result = await client.GetStringAsync($"http://subing-service/Sub?{queryString}");
            if (result != null)
            {
                resp = await PushIntoDatabase(numbers, operation, result);
            }
        }
        else
        {
            ViewBag.Error = "Invalid operation.";
            return View("Index");
        }
        ViewBag.ResultComment = resp;
        ViewBag.Result = result;
        await LoadDataAsync();
        return View("Index");
    }

    private async Task LoadDataAsync()
    {
        var client = _clientFactory.CreateClient("MyClient");
        var result = await client.GetStringAsync($"http://history-service/History");

        //Deserialize object to Dict<string,string>
        var rawRecords = JsonConvert.DeserializeObject<Dictionary<string, string>>(result);

        //Deserialize object to Dict<string,CalculationData>
        var records = new Dictionary<string, CalculationData>();

        // Iterate through keys in 'rawRecords', deserialize the JSON value to 'CalculationData', and add to 'records' dictionary.
        if (rawRecords != null)
        {
            foreach (var key in rawRecords.Keys)
            {
                var record = JsonConvert.DeserializeObject<CalculationData>(rawRecords[key]);
                records.Add(key, record);
            }
            ViewBag.ResultData = records;
        }

    }
    private async Task<string> PushIntoDatabase(List<int> input, string operation, string result)
    {
        var client = _clientFactory.CreateClient("MyClient");
        var data = new CalculationData()
        {
            Id = $"ListOfNumbers={string.Join(",", input)}&Operation={operation}&Result={result}",
            ListOfNumbers = input,
            Operation = operation,
            Result = Int32.Parse(result),
            Time = DateTime.Now
        };

        var jsonContent = JsonConvert.SerializeObject(data);
        var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        var response = await client.PostAsJsonAsync("http://history-service/History/DatabasePush", data);

        // Check the HTTP status code of the response:
        if (response.IsSuccessStatusCode)
        {
            return "Data successfully added to the database.";
        }
        else
        {
            return "Failed to insert data into the database.";
        }

    }
}
