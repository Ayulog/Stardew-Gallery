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
    void OpenFirstMatch();
    void HandleControllerBack();
}
