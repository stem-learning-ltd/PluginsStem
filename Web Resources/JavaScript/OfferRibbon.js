async function createActivity(primaryControl) {
    var formContext = primaryControl;

    var recordId = formContext.data.entity.getId();
    if (!recordId) {
        alert("No record Id found on the form.");
        return;
    }
    recordId = recordId.replace(/[{}]/g, "");

    var name = formContext.getAttribute("sam_name")?.getValue() || "";
    var description = formContext.getAttribute("sam_description")?.getValue() || "";
    var activityType = formContext.getAttribute("sam_activitytype")?.getValue();
    if (activityType == null) {
        alert("Please select an Activity Type (sam_activitytype).");
        return;
    }

    let offerOwnerId = null;
    try {
        var ownerVal = formContext.getAttribute("sam_offerowner")?.getValue();
        if (ownerVal && ownerVal.length > 0) {
            offerOwnerId = (ownerVal[0].id || "").replace(/[{}]/g, "");
        }
    } catch (e) {
        console.warn("Could not read sam_offerowner:", e);
    }

    async function fetchConnectedAccountIds(offerId) {
        const clientUrl = Xrm.Utility.getGlobalContext().getClientUrl();

        const fetchXml = `
          <fetch>
            <entity name="connection">
              <attribute name="record2id" />
              <filter type="and">
                <condition attribute="record1id" operator="eq" value="${offerId}" />
                <condition attribute="sam_type" operator="eq" value="7" />
              </filter>
            </entity>
          </fetch>`;

        const url = `${clientUrl}/api/data/v9.2/connections?fetchXml=${encodeURIComponent(fetchXml)}`;
        const res = await fetch(url, { headers: { "Accept": "application/json" } });
        if (!res.ok) {
            const text = await res.text();
            throw new Error(text || `Failed to fetch connections (${res.status})`);
        }
        const data = await res.json();

        const rows = Array.isArray(data.value) ? data.value : [];

        const ids = rows
            .filter(r => {
                const ln = r["_record2id_value@Microsoft.Dynamics.CRM.lookuplogicalname"];
                return ln ? ln.toLowerCase() === "account" : true;
            })
            .map(r =>
                (r._record2id_value || r["record2id"] || r["record2id_value"] || "")
                    .toString()
                    .replace(/[{}]/g, "")
            )
            .filter(Boolean);

        return ids;
    }

    let connectToIds = [];
    try {
        connectToIds = await fetchConnectedAccountIds(recordId);
    } catch (e) {
        console.error("Failed to load connected accounts:", e);
        alert("Couldn't load connected accounts. See console for details.");
        return;
    }

    if (connectToIds.length === 0) {
        //alert("⚠️ No connected accounts found with sam_type = 7.");
    }

    var payload = {
        recordID: recordId,
        name: name,
        description: description,
        activityType: activityType,
        connectToIDs: JSON.stringify(connectToIds),
        offerOwnerID: offerOwnerId
    };

    const clientUrl = Xrm.Utility.getGlobalContext().getClientUrl();
    const actionUrl = clientUrl + "/api/data/v9.2/sam_CreateActivityFromOffer";

    try {
        //alert("⏳ Your activity is being created. Please wait...");
        Xrm.Utility.showProgressIndicator("Your activity is being created...");

        const res = await fetch(actionUrl, {
            method: "POST",
            headers: {
                "OData-Version": "4.0",
                "Content-Type": "application/json; charset=utf-8",
                "Accept": "application/json"
            },
            body: JSON.stringify(payload)
        });

        const text = await res.text();
        if (!res.ok) throw new Error(text || `HTTP ${res.status}`);

        let activityId = null;
        try {
            const json = JSON.parse(text);
            activityId = json.activityID || json.activityId || null;
        } catch {
            console.warn("Response not JSON, cannot parse activityId.");
        }

        if (activityId) {
            alert("✅ Activity created! Redirecting...");
            Xrm.Utility.closeProgressIndicator();

            Xrm.Navigation.openForm({
                entityName: "sam_ambassadoractivity",
                entityId: activityId
            });
        } else {
            alert("✅ Activity created successfully, but no ID returned.");
        }
    } catch (err) {
        Xrm.Utility.closeProgressIndicator();
        console.error("Action error:", err);
        alert("❌ Error creating activity: " + err.message);
    }
}
