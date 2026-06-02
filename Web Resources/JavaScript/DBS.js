function onLoad(executionContext) {

    var formContext = executionContext.getFormContext();

    formContext.getAttribute("sam_dbsissuedate").addOnChange(setExpiryDate);

    setExpiryDate(executionContext);

}
function onSave(executionContext) {

    var formContext = executionContext.getFormContext();

    //setExpiryDate(executionContext);

}

function setExpiryDate(executionContext) {
    var formContext = executionContext.getFormContext();

    var issuedDateDBS = formContext.getAttribute("sam_dbsissuedate").getValue();

    if (issuedDateDBS)
    {
        var expiryDate = new Date(issuedDateDBS);

        expiryDate.setFullYear(expiryDate.getFullYear() + 3);
        expiryDate.setDate(expiryDate.getDate() - 1);

        formContext.getAttribute("sam_dbsexpirydate").setValue(expiryDate);

    }
    else {
        formContext.getAttribute("sam_dbsexpirydate").setValue(null);
    }
}