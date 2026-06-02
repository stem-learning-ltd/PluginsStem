function onLoad(executionContext)
{

    var formContext = executionContext.getFormContext();

    //formContext.getAttribute("sam_dbsissuedate").addOnChange(setExpiryDate);

    setAmbassadorSectionVisibility(executionContext);

}
function onSave(executionContext)
{

    var formContext = executionContext.getFormContext();


}

function setSectionVisibility(executionContext, tabName, sectionName, isVisible)
{
    var formContext = executionContext.getFormContext();

    var tab = formContext.ui.tabs.get(tabName);
    if (!tab)
    {
        return;
    }

    var section = tab.sections.get(sectionName);
    if (!section)
    {
        return;
    }

    section.setVisible(isVisible);
}

function setAmbassadorSectionVisibility(executionContext)
{
    var formContext = executionContext.getFormContext();

    var contactType = formContext.getAttribute("sic_websiteaccounttype").getValue();

    if (!contactType || !contactType.includes(157430000))
    {
        setSectionVisibility(executionContext, "tab_ambassador_information", "section_ambassador_information", false)
    }
    else
    {
        setSectionVisibility(executionContext, "tab_ambassador_information", "section_ambassador_information", true)
    }
}