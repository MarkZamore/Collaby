terraform {
  required_version = ">= 1.15.5"
  backend "kubernetes" {
    secret_suffix     = "collaby-cloudflare"
    namespace         = "collaby-system"
    in_cluster_config = true
  }
  required_providers {
    cloudflare = { source = "cloudflare/cloudflare", version = "~> 5.19" }
  }
}
provider "cloudflare" {}
