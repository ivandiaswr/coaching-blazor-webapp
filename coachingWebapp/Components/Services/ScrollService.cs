using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

public class ScrollService : IScrollService {

    private readonly IJSRuntime _jsRuntime;
    private readonly NavigationManager _navigationManager;
    private string _lastLocation;

    public ScrollService(IJSRuntime jsRuntime, NavigationManager navigationManager)
    {
        _jsRuntime = jsRuntime;
        _navigationManager = navigationManager;
        _lastLocation = _navigationManager.Uri;
    }

    public async Task ScrollToTop(){
         var currentUri = _navigationManager.Uri;
        
        if (!currentUri.Contains('#') && _lastLocation != currentUri)
        {
            await _jsRuntime.InvokeVoidAsync("window.scrollTo", 0, 0);
        }
        
        _lastLocation = currentUri;
    }

    public async Task ScrollToFragment()
    {
        await _jsRuntime.InvokeVoidAsync("scrollToFragment");
    }
}