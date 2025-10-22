# Berberis CrossBar Messaging

[![CI](https://github.com/azixaka/Berberis/actions/workflows/ci.yml/badge.svg)](https://github.com/azixaka/Berberis/actions/workflows/ci.yml)
[![Nuget](https://img.shields.io/nuget/v/Berberis.Messaging)](https://www.nuget.org/packages/Berberis.Messaging/)

Berberis CrossBar is a high-performance, allocation-free in-process message broker designed for creating complex, high-speed pipelines within a single process. Built on the concept of typed channels, Berberis CrossBar serves as the bridge connecting publishers and subscribers within your application.

## Features

- **Typed Channels**: Each channel in Berberis CrossBar is a typed destination, creating a clear and efficient path for message passing between publishers and subscribers.

- **Message Conflation**: Berberis CrossBar supports message conflation, enhancing the efficiency of your messaging system by preventing the overloading of channels with redundant or unnecessary data.

- **Record/Replay**: The broker provides a record/replay feature that can serialize a stream of updates into a stream. This serialized stream can then be deserialized and published on a channel, facilitating efficient data replay and debugging.

- **Comprehensive Observability**: With Berberis CrossBar, you can trace not only messages but also a wide array of statistics, including service time, latencies, rates, sources, percentiles, and more. This empowers you to gain deeper insights into the performance of your messaging system and make data-driven optimizations.

- **Stateful Channels**: Berberis CrossBar offers stateful channels, which store the latest published messages by key. This allows new subscribers to fetch the most recent state of the channel upon subscription, keeping everyone up-to-date and in sync.

- **Channel Reset and Message Deletions**: Berberis CrossBar also supports channel resets, allowing you to clear a channel and start fresh when necessary. Individual message deletions are also supported, ensuring that you have full control over the data in your channels.

- **Wildcard Subscriptions**: The system supports wildcard subscription patterns like '*' and '>', offering you more flexibility and control over the messages you subscribe to.

- **Metrics Export**: With the MetricsToJson extension method, you can easily generate a comprehensive JSON report of metrics from all CrossBar channels and each subscription. This feature provides an efficient way to monitor and optimize the performance of your messaging system.

## Getting Started

You can add Berberis CrossBar to your project through NuGet:

```sh
Install-Package Berberis.Messaging
```

## Quick Start

Here is a basic usage example:

```csharp
	ICrossBar xBar = new CrossBar();
	var destination = "number.inc";	
	
	using var subscription = xBar.Subscribe<int>(destination, msg => ProcessMessage(msg));
	
	for (int i = 0; i < 1000; i++)
	{
		xBar.Publish(destination, i);
	}
	
	ValueTask ProcessMessage(Message<long> message)
	{
		Console.WriteLine(message.Body);
		return ValueTask.CompletedTask;	
	}
	
	await subscription.MessageLoop;
```

Conflation and state fetching example:

```csharp	
	using var subscription = 
					xBar.Subscribe<int>(destination,
									 msg => ProcessMessage(msg),
									 fetchState: true,
									 TimeSpan.FromSeconds(1));	
```

For a more detailed guide on how to use Berberis CrossBar, please refer to our documentation.

Contributing
We appreciate any contributions to improve Berberis CrossBar. Please read our Contributing Guide for guidelines about how to proceed.

License
Berberis CrossBar is licensed under the GPL-3 license.

Contact
If you have any questions or suggestions, feel free to open an issue on GitHub.

Thank you for considering Berberis CrossBar for your messaging needs!