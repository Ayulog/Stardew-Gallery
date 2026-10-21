namespace StardewGallery;

internal interface IGalleryNavigation
{
    void OpenCharacter(string name);
    void OpenQuery(string query = "");
    void OpenEvent(EventIdentity identity);
    void OpenPhotos(EventIdentity identity);
    void OpenPrerequisite(string id);
    void OpenFilters();
    void ApplyFilters(QueryFilter filter);
    void Rename(EventIdentity identity);
    void Replay(EventIdentity identity);
    void ToggleUnlock();
    void Back();
    void Close();
}

internal interface IGallerySearchMenu
{
    bool IsSearchSelected { get; }
    void DeselectSearch();
    /// <summary>Enter while typing (keyboard or on-screen keyboard): keep the text and move focus to the results, without opening one.</summary>
    void FinishSearch();
    void HandleControllerBack();
}
