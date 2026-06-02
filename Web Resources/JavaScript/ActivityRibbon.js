function openHTMLPage(primaryControl) {
    var formContext = primaryControl;
    var recordId = formContext.data.entity.getId().replace(/[{}]/g, "");

    sessionStorage.setItem("sam_recordId", recordId);

    var pageInput = {
        pageType: "webresource",
        webresourceName: "sam_selectAccountConnections"
    };

    var navigationOptions = {
        target: 2,
        width: { value: 600, unit: "px" },
        height: { value: 500, unit: "px" },
        position: 1
    };

    Xrm.Navigation.navigateTo(pageInput, navigationOptions)
        .catch(e => console.error("navigateTo error:", e));
}
