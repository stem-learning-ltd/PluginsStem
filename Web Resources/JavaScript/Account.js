function onLoad(executionContext)
{

    var formContext = executionContext.getFormContext();

    //formContext.getAttribute("sam_dbsissuedate").addOnChange(setExpiryDate);

    setAmbassadorSubgridVisibility(executionContext);

}
function onSave(executionContext)
{

    var formContext = executionContext.getFormContext();


}

function setSectionVisibility(executionContext, tabName, sectionName, isVisible)
{
    var formContext = executionContext.getFormContext();

    var tab = formContext.ui.tabs.get(tabName);
    if (!tab) {
        return;
    }

    var section = tab.sections.get(sectionName);
    if (!section) {
        return;
    }

    section.setVisible(isVisible);
}

function setSubgridVisibility(executionContext, subgridName, isVisible)
{
    var formContext = executionContext.getFormContext();

    var subgridControl = formContext.getControl(subgridName);
    if (!subgridControl) {
        return;
    }

    subgridControl.setVisible(isVisible);
}

function setTabVisibility(executionContext, tabName, isVisible) {
    var formContext = executionContext.getFormContext();

    var tab = formContext.ui.tabs.get(tabName);
    if (!tab) {
        console.warn("Tab '" + tabName + "' not found on form.");
        return;
    }

    tab.setVisible(isVisible);
}


function setAmbassadorSubgridVisibility(executionContext)
{
    var formContext = executionContext.getFormContext();

    var accountType = formContext.getAttribute("sic_organisationtype").getValue();

    setSubgridVisibility(executionContext, "scheme_ambassadors", false);
    setSubgridVisibility(executionContext, "institution_ambassadors", false);

    if (accountType && accountType.includes(999990001)) {
        setSubgridVisibility(executionContext, "scheme_ambassadors", true);
    }
    else if (accountType && accountType.includes(157430004)) {
        setSubgridVisibility(executionContext, "institution_ambassadors", true);
    }
    else
    {
        setTabVisibility(executionContext, "account_ambassadors", false);
    }
}