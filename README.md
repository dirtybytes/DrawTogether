# DrawTogether

A very simple website project using SkiaSharp/Skia CanvasKit, SignalR, and ASP.NET Core for drawing online with multiple users on the same canvas.

## Using the website

* To run the project:

```
cd DrawTogetherMvc
dotnet run
```

Optionally, if you haven't done this already, run the following command to setup the self-signed security certificate:

```
dotnet dev-certs https --trust
```

Then navigate to [https://localhost:5001/](https://localhost:5001/) in your browser and start drawing.

## Known limitations

Skia CanvasKit will consume a lot of memory on a page reload, especially on Firefox where it doesn't seem to be reclaimed even after closing the tab, so try not to reload unnecessarily.
