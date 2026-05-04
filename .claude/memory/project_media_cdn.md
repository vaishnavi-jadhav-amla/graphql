---
name: Media Delivery via CDN
description: Product images and media are NEVER served from the API or the application server. All media URLs are CDN-generated paths. API returns paths, CDN serves bytes.
type: project
---

## Rule

The GraphQL API never transfers image or media bytes. It returns URL paths only. All actual image requests go to a CDN.

## URL Construction

```csharp
// Config in appsettings.json → GraphQL.Media
"Media": {
  "CdnUrl": "https://cdn.example.com/znode",
  "MediaServerUrl": "https://media.example.com",
  "MediaServerThumbnailUrl": "https://media.example.com/thumbs",
  "ImageFormats": ["webp", "jpg"]
}
```

API returns:
```json
{
  "imageUrl": "https://cdn.example.com/znode/products/500/drill-18v.webp",
  "thumbnailUrl": "https://cdn.example.com/znode/thumbs/drill-18v_150x150.webp"
}
```

## Responsibilities

| Layer | Responsibility |
|---|---|
| **GraphQL API** | Return CDN path from `ImageName` + `MediaConfig` prefix. No byte transfer. |
| **CDN** | Cache images globally. 24h+ TTL. Image transformation (resize, format conversion). |
| **Origin / media store** | Stores originals. Only hit on CDN miss. |
| **Admin publish pipeline** | Upload new images to origin, invalidate CDN paths. |

## Responsive Image Support

API returns a `MediaType` with multiple renditions:

```graphql
type MediaType {
  id: String!
  alt: String
  url: String!                       # base/original
  thumbnailUrl: String
  srcSet: [MediaVariant!]!           # for <img srcset>
}
type MediaVariant {
  width: Int!
  url: String!
  format: String!                    # webp, jpg
}
```

Client uses `srcSet` for responsive loading. CDN handles the actual variant generation (on-demand or pre-generated).

## Cloud Provider Options

| Provider | Notes |
|---|---|
| **Azure Front Door + Blob Storage** | Matches Azure-first infra. Use Image Resizer worker. |
| **Cloudflare Images** | Built-in transform + resize. Per-image pricing. |
| **AWS CloudFront + S3 + Lambda@Edge** | For AWS deploys. |

**Decision: Azure Front Door + Blob Storage** for production.

## Image Domains Whitelist

Security: the GraphQL API must only return image URLs from approved hosts. Reject or rewrite any absolute URL coming from legacy data:

```csharp
public string NormalizeImageUrl(string? raw, string? imageName)
{
    if (string.IsNullOrEmpty(imageName)) return DefaultPlaceholder;
    // If ImageName is a full URL that isn't on our CDN, treat as path only
    if (Uri.TryCreate(imageName, UriKind.Absolute, out var uri)
        && !uri.Host.EndsWith("cdn.example.com"))
    {
        imageName = Path.GetFileName(uri.LocalPath);
    }
    return $"{_mediaConfig.CdnUrl.TrimEnd('/')}/products/{imageName}";
}
```

## Cache Invalidation

On product image update:
1. Admin writes new image to blob storage (versioned filename OR overwrite).
2. Publish step issues CDN purge for the affected path.
3. Use versioned filenames (`drill-18v_v2.webp`) rather than purge-on-overwrite when possible — simpler, no purge latency.

## Rules

- Never use `data:` URIs for any media in GraphQL responses — bloats payloads.
- Never proxy image bytes through the GraphQL API or Next.js BFF.
- Thumbnails are pre-computed on publish OR generated on-demand by CDN — never by the GraphQL API.
- `MediaServerUrl` in GraphQL response may be empty if the CDN path is absolute; frontend must handle both.
