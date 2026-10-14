resource "cloudflare_zero_trust_tunnel_cloudflared" "collaby" {
  account_id = var.account_id
  name       = var.tunnel_name
  config_src = "cloudflare"
}
resource "cloudflare_zero_trust_tunnel_cloudflared_config" "collaby" {
  account_id = var.account_id
  tunnel_id  = cloudflare_zero_trust_tunnel_cloudflared.collaby.id
  config = {
    ingress = [
      { hostname = var.zone_name, service = "http://frontend.collaby.svc.cluster.local:80" },
      { service = "http_status:404" }
    ]
  }
}
resource "cloudflare_dns_record" "collaby" {
  zone_id = var.zone_id
  name    = var.zone_name
  type    = "CNAME"
  content = "${cloudflare_zero_trust_tunnel_cloudflared.collaby.id}.cfargotunnel.com"
  proxied = true
  ttl     = 1
}
data "cloudflare_zero_trust_tunnel_cloudflared_token" "collaby" {
  account_id = var.account_id
  tunnel_id  = cloudflare_zero_trust_tunnel_cloudflared.collaby.id
}
