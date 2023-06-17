# Berberis CrossBar Messaging

[![Nuget](https://img.shields.io/nuget/v/Berberis.Messaging)](https://www.nuget.org/packages/Berberis.Messaging/)

Berberis CrossBar is a high-performance, allocation-free in-process message broker designed for creating complex, high-speed pipelines within a single process. Built on the concept of typed channels, Berberis CrossBar serves as the bridge connecting publishers and subscribers within your application.

## Features

- **Typed Channels**: Each channel in Berberis CrossBar is a typed destination, creating a clear and efficient path for message passing between publishers and subscribers.

- **Message Conflation**: Berberis CrossBar supports message conflation, enhancing the efficiency of your messaging system by preventing the overloading of channels with redundant or unnecessary data.

- **Record/Replay**: The broker provides a record/replay feature that can serialize a stream of updates into a stream. This serialized stream can then be deserialized and published on a channel, facilitating efficient data replay and debugging.

- **Comprehensive Observability**: With Berberis CrossBar, you can trace not only messages but also a wide array of statistics, including service time, latencies, rates, sources, percentiles, and more. This empowers you to gain deeper insights into the performance of your messaging system and make data-driven optimizations.

## Getting Started

You can add Berberis CrossBar to your project through NuGet:

```sh
Install-Package Berberis.Messaging
```

For a more detailed guide on how to use Berberis CrossBar, please refer to our documentation.

Contributing
We appreciate any contributions to improve Berberis CrossBar. Please read our Contributing Guide for guidelines about how to proceed.

License
Berberis CrossBar is licensed under the MIT license.

Contact
If you have any questions or suggestions, feel free to open an issue on GitHub.

Thank you for considering Berberis CrossBar for your messaging needs!


Please replace the `()` in `[documentation]()`, `[Contributing Guide]()`, and `[MIT license]()` with the actual links to your documentation, contributing guide, and license file respectively, if available.

This README.md provides a brief description of your project, highlights its main features, and gives a simple guide on how to get started with the project. It also provides information on how to contribute to the project, its license, and how to contact you. This should give your users a good overview of what Berberis CrossBar is and how they can use it.
