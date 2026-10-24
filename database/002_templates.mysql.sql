CREATE TABLE IF NOT EXISTS realm_template (
    id INT NOT NULL PRIMARY KEY,
    group_id INT NOT NULL,
    name VARCHAR(64) NOT NULL,
    stage VARCHAR(64) NOT NULL,
    `rank` INT NOT NULL UNIQUE,
    description VARCHAR(500) NOT NULL,
    skill_slots INT NOT NULL DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS item_option_template (
    id INT NOT NULL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    type INT NOT NULL DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS item_template (
    id SMALLINT NOT NULL PRIMARY KEY,
    type INT NOT NULL DEFAULT 0,
    skill INT NOT NULL DEFAULT 0,
    gender INT NOT NULL DEFAULT 3,
    name VARCHAR(100) NOT NULL,
    sub_name VARCHAR(100) NOT NULL DEFAULT '',
    description VARCHAR(500) NOT NULL DEFAULT '',
    level INT NOT NULL DEFAULT 1,
    icon_id INT NOT NULL DEFAULT 0,
    part INT NOT NULL DEFAULT -1,
    is_up_to_up BOOLEAN NOT NULL DEFAULT FALSE,
    is_drop BOOLEAN NOT NULL DEFAULT FALSE,
    `require` BIGINT NOT NULL DEFAULT 0,
    is_expires BOOLEAN NOT NULL DEFAULT FALSE,
    seconds_expires BIGINT NOT NULL DEFAULT 0,
    sale_coin BIGINT NOT NULL DEFAULT 0,
    options JSON NOT NULL,
    class_id INT NOT NULL DEFAULT 0,
    realm_id INT NOT NULL,
    element_id INT NOT NULL DEFAULT 0,
    rarity INT NOT NULL DEFAULT 0,
    max_stack INT NOT NULL DEFAULT 1,
    asset_key VARCHAR(200) NOT NULL DEFAULT '',
    effect_stat_id INT NOT NULL DEFAULT 0,
    effect_param INT NOT NULL DEFAULT 0,
    duration_seconds INT NOT NULL DEFAULT 0,
    restore_percent INT NOT NULL DEFAULT 0,
    CONSTRAINT fk_item_realm FOREIGN KEY(realm_id) REFERENCES realm_template(id),
    CONSTRAINT ck_item_class CHECK(class_id BETWEEN 0 AND 5),
    CONSTRAINT ck_item_element CHECK(element_id BETWEEN 0 AND 5),
    CONSTRAINT ck_item_stack CHECK(max_stack > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS map_template (
    id INT NOT NULL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    type INT NOT NULL DEFAULT 0,
    realm_id INT NOT NULL,
    max_player INT NOT NULL DEFAULT 50,
    zone_number INT NOT NULL DEFAULT 4,
    width INT NOT NULL,
    height INT NOT NULL,
    spawn_x INT NOT NULL,
    spawn_y INT NOT NULL,
    tile_id INT NOT NULL DEFAULT 0,
    bg_id INT NOT NULL DEFAULT 0,
    bg_type INT NOT NULL DEFAULT 0,
    is_double BOOLEAN NOT NULL DEFAULT FALSE,
    teleport INT NOT NULL DEFAULT 0,
    waypoint JSON NOT NULL,
    mob JSON NOT NULL,
    arrNpc JSON NOT NULL,
    background JSON NOT NULL,
    action JSON NOT NULL,
    map_collisions JSON NOT NULL,
    asset_key VARCHAR(200) NOT NULL DEFAULT '',
    CONSTRAINT fk_map_realm FOREIGN KEY(realm_id) REFERENCES realm_template(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS npc_template (
    id SMALLINT NOT NULL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    head INT NOT NULL DEFAULT 0,
    body INT NOT NULL DEFAULT 0,
    leg INT NOT NULL DEFAULT 0,
    avatar INT NOT NULL DEFAULT 0,
    menu JSON NOT NULL,
    chats JSON NOT NULL,
    realm_id INT NOT NULL,
    role VARCHAR(32) NOT NULL,
    asset_key VARCHAR(200) NOT NULL DEFAULT '',
    CONSTRAINT fk_npc_realm FOREIGN KEY(realm_id) REFERENCES realm_template(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS skill_template (
    id SMALLINT NOT NULL PRIMARY KEY,
    class_id INT NOT NULL,
    name VARCHAR(100) NOT NULL,
    max_point INT NOT NULL DEFAULT 10,
    mana_use_type INT NOT NULL DEFAULT 0,
    type INT NOT NULL DEFAULT 0,
    icon_id INT NOT NULL DEFAULT 0,
    damage_info VARCHAR(200) NOT NULL DEFAULT '',
    description VARCHAR(500) NOT NULL DEFAULT '',
    cooldown INT NOT NULL DEFAULT 0,
    realm_id INT NOT NULL,
    element_id INT NOT NULL DEFAULT 0,
    asset_key VARCHAR(200) NOT NULL DEFAULT '',
    skill_data_templates JSON NOT NULL,
    CONSTRAINT fk_skill_realm FOREIGN KEY(realm_id) REFERENCES realm_template(id),
    CONSTRAINT ck_skill_class CHECK(class_id BETWEEN 1 AND 5),
    CONSTRAINT ck_skill_element CHECK(element_id BETWEEN 0 AND 5)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

