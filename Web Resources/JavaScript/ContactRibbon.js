function openHTMLPage(primaryControl) {
    var formContext = primaryControl;
    var recordId = formContext.data.entity.getId().replace(/[{}]/g, "");

    sessionStorage.setItem("sam_recordId", recordId);

    var pageInput = {
        pageType: "webresource",
        webresourceName: "sam_selectAccountContact"
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

async function exportIdCardsZip(primaryControl, selectedControl, selectedAllItemReferences, selectedItemReferences) {
    await ensureLibraries();

    const clientUrl = Xrm.Utility.getGlobalContext().getClientUrl();

    const contactIds = getSelectedContactIds(primaryControl, selectedControl, selectedAllItemReferences, selectedItemReferences);

    if (!contactIds || contactIds.length === 0) {
        alert("Select at least one contact.");
        return;
    }

    const contacts = await fetchContactsForCsv(clientUrl, contactIds);

    const csv = buildCsv(contacts);

    const zip = new JSZip();
    zip.file("contacts.csv", csv);

    for (const c of contacts) {
        const id = (c.contactid || "").replace(/[{}]/g, "");
        if (!id) continue;

        const blob = await downloadContactImageBlob(clientUrl, id, "sam_profileimage");
        if (!blob) continue;

        zip.file(`${id}.jpeg`, blob);
    }

    const content = await zip.generateAsync({ type: "blob" });

    saveAs(content, "ID_Cards_Pending_Order.zip");
}

async function fetchContactsForCsv(clientUrl, ids) {
    const chunks = chunkArray(ids, 25);
    const all = [];

    // 1) Load contacts
    for (const chunk of chunks) {
        const filter = chunk
            .map(id => `contactid eq ${formatGuidForOData(id)}`)
            .join(" or ");

        const select = [
            "contactid",
            "firstname",
            "lastname",
            "sam_dateofbirth",
            "address1_line1",
            "address1_city",
            "address1_postalcode",
            "_si_titleid_value"
        ].join(",");

        // =========================
        // UPDATED
        // sam_hub -> sam_deliverypartner
        // =========================

        const expand = [
            "sam_DeliveryPartner($select=sam_name,sam_emailaddress,sam_websiteaddress,sam_phonenumber)"
        ].join(",");

        const url =
            `${clientUrl}/api/data/v9.2/contacts` +
            `?$select=${select}` +
            `&$expand=${expand}` +
            `&$filter=${encodeURIComponent(filter)}`;

        const res = await fetch(url, {
            headers: {
                "Accept": "application/json",
                "Prefer": "odata.include-annotations=*"
            }
        });

        if (!res.ok) {
            const text = await res.text();
            throw new Error(text || `Failed to load contacts (${res.status})`);
        }

        const data = await res.json();

        all.push(...(data.value || []));
    }

    // Helper: resolve Entity Set Name from logical name
    async function getEntitySetName(logicalName) {
        const metaUrl =
            `${clientUrl}/api/data/v9.2/EntityDefinitions(LogicalName='${logicalName}')?$select=EntitySetName`;

        const res = await fetch(metaUrl, {
            headers: { "Accept": "application/json" }
        });

        if (!res.ok)
            return null;

        const data = await res.json();

        return data && data.EntitySetName
            ? data.EntitySetName
            : null;
    }

    // 2) Load DBS records and attach best one to each contact
    const contactIdSet = new Set(
        all
            .map(c => (c.contactid || "").replace(/[{}]/g, "").toLowerCase())
            .filter(Boolean)
    );

    const contactIdList = Array.from(contactIdSet);

    const dbsByContactId = {};

    const dbsChunks = chunkArray(contactIdList, 25);

    const dbsEntitySet =
        (await getEntitySetName("sam_dbs")) || "sam_dbses";

    async function loadDbsForChunk(chunk) {
        const filter = chunk
            .map(id =>
                `(_sam_dbscontact_value eq ${formatGuidForOData(id)} and statecode eq 0)`)
            .join(" or ");

        const select = [
            "sam_dbsissuedate",
            "sam_dbsexpirydate",
            "_sam_dbscontact_value"
        ].join(",");

        const url =
            `${clientUrl}/api/data/v9.2/${dbsEntitySet}` +
            `?$select=${select}` +
            `&$filter=${encodeURIComponent(filter)}` +
            `&$orderby=sam_dbsexpirydate desc`;

        const res = await fetch(url, {
            headers: {
                "Accept": "application/json",
                "Prefer": "odata.include-annotations=*"
            }
        });

        if (!res.ok) {
            const text = await res.text();
            throw new Error(text || `Failed to load DBS records (${res.status})`);
        }

        const data = await res.json();

        const rows = data.value || [];

        for (const d of rows) {
            const cid =
                (d._sam_dbscontact_value || "")
                    .toString()
                    .toLowerCase();

            if (!cid)
                continue;

            if (!dbsByContactId[cid]) {
                dbsByContactId[cid] = d;
            }
        }
    }

    for (const chunk of dbsChunks) {
        await loadDbsForChunk(chunk);
    }

    for (const c of all) {
        const cid =
            (c.contactid || "")
                .replace(/[{}]/g, "")
                .toLowerCase();

        const d = dbsByContactId[cid];

        c.__dbsissuedate =
            d ? d.sam_dbsissuedate : null;

        c.__dbsissuedate_formatted =
            d
                ? (d["sam_dbsissuedate@OData.Community.Display.V1.FormattedValue"] || "")
                : "";

        c.__dbsexpirydate =
            d ? d.sam_dbsexpirydate : null;

        c.__dbsexpirydate_formatted =
            d
                ? (d["sam_dbsexpirydate@OData.Community.Display.V1.FormattedValue"] || "")
                : "";
    }

    return all;
}

function getSelectedContactIds(primaryControl, selectedControl, selectedAllItemReferences, selectedItemReferences) {
    const refs = selectedItemReferences || selectedAllItemReferences;

    if (!refs || refs.length === 0) {
        const gridControl = selectedControl || primaryControl;

        const selectedRows =
            gridControl && gridControl.getGrid
                ? gridControl.getGrid().getSelectedRows()
                : null;

        if (selectedRows && selectedRows.getLength() > 0) {
            return selectedRows.map(row =>
                row.getData().getEntity().getId().replace(/[{}]/g, ""));
        }
    }
    else {
        return refs.map(r =>
            r.Id.replace(/[{}]/g, ""));
    }

    return [];
}

function extractIdsFromGridControl(ctrl) {
    const ids = [];

    try {
        const grid =
            ctrl && ctrl.getGrid
                ? ctrl.getGrid()
                : null;

        if (!grid)
            return ids;

        const selected = grid.getSelectedRows();

        if (!selected || selected.getLength() === 0)
            return ids;

        selected.forEach(function (row) {
            const id =
                (row.getData().getEntity().getId() || "")
                    .replace(/[{}]/g, "");

            if (id)
                ids.push(id);
        });
    }
    catch (e) { }

    return ids;
}

function buildCsv(rows) {
    const header = [
        "ContactID",
        "Title",
        "First name",
        "Surname",
        "Primary Address",
        "City",
        "County",
        "Postcode",
        "Title",
        "Firstname",
        "Surname",
        "DOB",
        "DBS Issued",
        "DBS Valid To",
        "Hub Name",
        "Hub Phone",
        "Hub Email",
        "Hub website"
    ];

    const lines = [header.join(",")];

    for (const r of rows) {
        const titleFormatted =
            r["_si_titleid_value@OData.Community.Display.V1.FormattedValue"] || "";

        const dbsIssuedFormatted =
            r.__dbsissuedate_formatted ||
            r.__dbsissuedate ||
            "";

        const dbsExpiryFormatted =
            r.__dbsexpirydate_formatted ||
            r.__dbsexpirydate ||
            "";

        // =========================
        // UPDATED
        // Delivery Partner values
        // =========================

        const deliveryPartnerName =
            r.sam_deliverypartner?.sam_name || "";

        const deliveryPartnerEmail =
            r.sam_deliverypartner?.sam_emailaddress || "";

        const deliveryPartnerWebsite =
            r.sam_deliverypartner?.sam_websiteaddress || "";

        const deliveryPartnerPhone =
            r.sam_deliverypartner?.sam_phonenumber || "";

        const dobFormatted = r.sam_dateofbirth
            ? new Date(r.sam_dateofbirth).toLocaleDateString("en-GB")
            : "";

        const values = [
            r.contactid || "",

            (titleFormatted || "").toUpperCase(),
            (r.firstname || "").toUpperCase(),
            (r.lastname || "").toUpperCase(),     
            (r.address1_line1 || "").toUpperCase(),
            (r.address1_city || "").toUpperCase(),
            "",
            (r.address1_postalcode || "").toUpperCase(),

            titleFormatted || "",
            r.firstname || "",
            r.lastname || "",
            dobFormatted,
            dbsIssuedFormatted || "",
            dbsExpiryFormatted || "",

            deliveryPartnerName || "",
            deliveryPartnerPhone || "",
            deliveryPartnerEmail || "",
            deliveryPartnerWebsite || ""
        ].map(csvEscape);

        lines.push(values.join(","));
    }

    return lines.join("\r\n");
}

function csvEscape(v) {
    const s = (v ?? "").toString();

    if (
        s.includes('"') ||
        s.includes(",") ||
        s.includes("\n") ||
        s.includes("\r")
    ) {
        return `"${s.replace(/"/g, '""')}"`;
    }

    return s;
}

async function downloadContactImageBlob(clientUrl, contactId, imageAttribute) {
    const imageUrl =
        `${clientUrl}/Image/download.aspx?Entity=contact&Attribute=${imageAttribute}&Id=${contactId}&Full=true`;

    const res = await fetch(imageUrl, {
        method: "GET",
        headers: { "Accept": "*/*" }
    });

    if (!res.ok) {
        console.error("Error fetching image for contact:", contactId);
        return null;
    }

    return await res.blob();
}

function chunkArray(arr, size) {
    const out = [];

    for (let i = 0; i < arr.length; i += size) {
        out.push(arr.slice(i, i + size));
    }

    return out;
}

function formatGuidForOData(id) {
    const clean =
        (id || "").replace(/[{}]/g, "");

    return clean;
}

function loadScript(url) {
    return new Promise((resolve, reject) => {
        var s = document.createElement("script");

        s.src = url;
        s.onload = resolve;
        s.onerror = reject;

        document.head.appendChild(s);
    });
}

async function ensureLibraries() {
    if (typeof JSZip === "undefined") {
        await loadScript(
            "https://cdnjs.cloudflare.com/ajax/libs/jszip/3.10.1/jszip.min.js");
    }

    if (typeof saveAs === "undefined") {
        await loadScript(
            "https://cdnjs.cloudflare.com/ajax/libs/FileSaver.js/2.0.5/FileSaver.min.js");
    }
}