# REST → GraphQL mapping (B-Store)

**Default GraphQL URL (https profile):** `https://localhost:7041/graphql`  
**HTTP profile:** `http://localhost:5249/graphql`  

Paste the **GraphQL document** into Banana Cake Pop’s **Operation** tab (not JSON). Use **GraphQL Variables** when `$variables` appear.  
**cURL:** use `-k` if your dev cert is self-signed.

Input types (`CopyBStoreRequestInput`, `UpdateBStoreRequestInput`, etc.) must match your live schema—use **Schema** / introspection in Banana Cake Pop if a name differs.

---

| REST | GraphQL (Operation tab — executable) | cURL (JSON POST, `https://localhost:7041/graphql`) |
|------|----------------------------------------|-----------------------------------------------------|
| **GET** `/v2/b-stores/parent-portal/{portalId}/users/{userId}/stores` | `query($portalId:Int!,$userId:Int!){bStoreList(portalId:$portalId,userId:$userId){isBStoreManager isBStoreOwner bStores{portalId storeName domainURL isActive}}}` + variables `{"portalId":1,"userId":1}` | `curl -k -sS -X POST "https://localhost:7041/graphql" -H "Content-Type: application/json" -d "{\"query\":\"query($portalId:Int!,$userId:Int!){bStoreList(portalId:$portalId,userId:$userId){isBStoreManager isBStoreOwner bStores{portalId storeName}}}\",\"variables\":{\"portalId\":1,\"userId\":1}}"` |
| **GET** `/v2/b-stores/{storeId}` | `query($storeId:Int!){bStore(storeId:$storeId){portalId storeName isActive}}` + `{"storeId":42}` | `curl -k -sS -X POST "https://localhost:7041/graphql" -H "Content-Type: application/json" -d "{\"query\":\"query($s:Int!){bStore(storeId:$s){portalId storeName}}\",\"variables\":{\"s\":42}}"` |
| **GET** `/v2/b-stores/{storeId}/theme` | `query($storeId:Int!){bStoreTheme(storeId:$storeId){portalId}}` + `{"storeId":42}` | `curl -k -sS -X POST "https://localhost:7041/graphql" -H "Content-Type: application/json" -d "{\"query\":\"query($s:Int!){bStoreTheme(storeId:$s){portalId}}\",\"variables\":{\"s\":42}}"` |
| **PUT** `/v2/b-stores/{storeId}/theme` | `mutation($storeId:Int!,$input:BStoresDesignRequestModelV2Input!){bStoreThemeUpdate(storeId:$storeId,input:$input)}` + variables with real design fields | `curl -k -sS -X POST "https://localhost:7041/graphql" -H "Content-Type: application/json" -d "{\"query\":\"mutation($s:Int!,$i:BStoresDesignRequestModelV2Input!){bStoreThemeUpdate(storeId:$s,input:$i)}\",\"variables\":{\"s\":42,\"i\":{}}}"` |
| **GET** `/v2/b-stores/parent-portal/{portalId}/catalogs` | `query($portalId:Int!){bStoreCatalogs(portalId:$portalId,associated:true,pageIndex:1,pageSize:10){__typename}}` | `curl -k -sS -X POST "https://localhost:7041/graphql" -H "Content-Type: application/json" -d "{\"query\":\"query($p:Int!){bStoreCatalogs(portalId:$p,associated:true,pageIndex:1,pageSize:10){__typename}}\",\"variables\":{\"p\":1}}"` |
| **POST** `/v2/b-stores/parent-portal/{portalId}/users/{userId}/setup` | `mutation($portalId:Int!,$userId:Int!,$input:BStoresWebStoreCreateRequestV2Input!){bStoreCreate(portalId:$portalId,userId:$userId,input:$input){__typename}}` + fill `$input` from schema | `curl -k -sS -X POST "https://localhost:7041/graphql" -H "Content-Type: application/json" -d "{\"query\":\"mutation($p:Int!,$u:Int!,$i:BStoresWebStoreCreateRequestV2Input!){bStoreCreate(portalId:$p,userId:$u,input:$i){__typename}}\",\"variables\":{\"p\":1,\"u\":1,\"i\":{}}}"` |
| **POST** `/v2/b-stores/{sourcePortalId}/copy` | `mutation($sourcePortalId:Int!,$input:CopyBStoreRequestInput!){bStoreCopy(sourcePortalId:$sourcePortalId,input:$input)}` + `$input` from schema | `curl -k -sS -X POST "https://localhost:7041/graphql" -H "Content-Type: application/json" -d "{\"query\":\"mutation($s:Int!,$i:CopyBStoreRequestInput!){bStoreCopy(sourcePortalId:$s,input:$i)}\",\"variables\":{\"s\":1,\"i\":{}}}"` |
| **POST** `/v2/b-stores/{storeId}/users/{userId}/set-activation?active={val}` | `mutation($storeId:Int!,$userId:Int!,$active:Boolean!){bStoreSetActivation(storeId:$storeId,userId:$userId,active:$active)}` + `{"storeId":1,"userId":2,"active":true}` | `curl -k -sS -X POST "https://localhost:7041/graphql" -H "Content-Type: application/json" -d "{\"query\":\"mutation($s:Int!,$u:Int!,$a:Boolean!){bStoreSetActivation(storeId:$s,userId:$u,active:$a)}\",\"variables\":{\"s\":1,\"u\":2,\"a\":true}}"` |
| **GET** `/v2/b-stores/parent-portal/{portalId}/domain-name` | `query($portalId:Int!){bStoreDomainNameSuffix(portalId:$portalId){__typename}}` | `curl -k -sS -X POST "https://localhost:7041/graphql" -H "Content-Type: application/json" -d "{\"query\":\"query($p:Int!){bStoreDomainNameSuffix(portalId:$p){__typename}}\",\"variables\":{\"p\":1}}"` |
| **PUT** `/v2/b-stores/{storeId}` | `mutation($storeId:Int!,$input:UpdateBStoreRequestInput!){bStoreUpdate(storeId:$storeId,input:$input)}` + `$input` from schema | `curl -k -sS -X POST "https://localhost:7041/graphql" -H "Content-Type: application/json" -d "{\"query\":\"mutation($s:Int!,$i:UpdateBStoreRequestInput!){bStoreUpdate(storeId:$s,input:$i)}\",\"variables\":{\"s\":42,\"i\":{}}}"` |
| **GET** `/Domain/List` | `query{domainList(pageIndex:1,pageSize:50){__typename}}` | `curl -k -sS -X POST "https://localhost:7041/graphql" -H "Content-Type: application/json" -d "{\"query\":\"query{domainList(pageIndex:1,pageSize:50){__typename}}\"}"` |
| **POST** `/fileupload/post` | GraphQL **Upload** = **multipart** (not plain JSON). Use Banana Cake Pop file picker or Postman **GraphQL** body type **multipart**. Field: `mutation($f: Upload!){ bStoreUploadFile(file: $f) { __typename } }` | See **Multipart cURL** below |
| **POST** `/FileUpload/remove` | `mutation{ bStoreRemoveUploadedFile(mediaIds: \"1,2,3\") }` | `curl -k -sS -X POST "https://localhost:7041/graphql" -H "Content-Type: application/json" -d "{\"query\":\"mutation{bStoreRemoveUploadedFile(mediaIds:\\\"1,2,3\\\")}\"}"` |
| **GET** `/v2/b-stores/parent-portal/{portalId}/price-list` | `query($portalId:Int!){bStorePriceLists(portalId:$portalId,associated:true,pageIndex:1,pageSize:10){__typename}}` | `curl -k -sS -X POST "https://localhost:7041/graphql" -H "Content-Type: application/json" -d "{\"query\":\"query($p:Int!){bStorePriceLists(portalId:$p,associated:true,pageIndex:1,pageSize:10){__typename}}\",\"variables\":{\"p\":1}}"` |

---

## Optional: EF-backed reads (not 1:1 with REST table; SQL `IQueryable`)

| Concept | GraphQL (Operation) |
|---------|----------------------|
| Portals / catalogs / price lists / domains from DB | `query { bStorePortalsFromDatabase { portalId storeName } }`, `bStoreCatalogAssignmentsFromDatabase(where:{portalId:{eq:1}}){catalogName}`, etc. |

---

## `bStoreUploadFile` (Upload)

GraphQL `Upload` is **multipart/form-data** (`operations`, `map`, file parts). One-line JSON cURL does **not** work. Use **Banana Cake Pop** / **Postman GraphQL** file attachment, or generate cURL from Postman after attaching the file.

---

## Postman

1. **Method:** POST  
2. **URL:** `https://localhost:7041/graphql`  
3. **Body:** `GraphQL` (or `raw` JSON like the cURL `-d` payloads above).  
4. **SSL certificate verification:** off for local dev if needed.
