CREATE TABLE IF NOT EXISTS monster_template (
    id INT PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    hp BIGINT NOT NULL,
    damage BIGINT NOT NULL,
    defence BIGINT NOT NULL,
    speed INT NOT NULL,
    attack_range INT NOT NULL,
    aggro_range INT NOT NULL,
    leash_range INT NOT NULL,
    attack_cooldown INT NOT NULL,
    respawn_seconds INT NOT NULL,
    tu_vi BIGINT NOT NULL,
    drop_item_id SMALLINT NOT NULL,
    drop_quantity INT NOT NULL,
    drop_chance INT NOT NULL,
    FOREIGN KEY (drop_item_id) REFERENCES item_template(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin;

CREATE TABLE IF NOT EXISTS monster_kill (
    life_id CHAR(32) PRIMARY KEY,
    character_id INT NOT NULL,
    map_id INT NOT NULL,
    zone_id INT NOT NULL,
    tu_vi BIGINT NOT NULL,
    killed_at BIGINT NOT NULL,
    INDEX ix_kill_time(killed_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin;

CREATE TABLE IF NOT EXISTS item_map (
    id CHAR(32) PRIMARY KEY,
    player_id INT NOT NULL,
    map_id INT NOT NULL,
    zone_id INT NOT NULL,
    item_id SMALLINT NOT NULL,
    quantity INT NOT NULL,
    x FLOAT NOT NULL,
    y FLOAT NOT NULL,
    owner_time BIGINT NOT NULL,
    left_time BIGINT NOT NULL,
    INDEX ix_item_map_zone(map_id, zone_id, left_time)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin;

INSERT IGNORE INTO monster_template VALUES
(1,'Thanh Lang',240,160,30,70,85,240,420,1500,15,10,3010,1,100),
(2,'Xích Hầu',400,190,40,75,90,250,440,1500,18,15,3010,1,100),
(3,'Sơn Yêu',650,220,50,75,90,250,440,1400,20,20,3010,1,100),
(4,'Huyết Lang',900,260,65,80,95,260,460,1400,22,25,3010,1,100),
(5,'U Hồn',1200,300,80,80,95,260,460,1300,25,30,3010,1,100),
(6,'Thạch Linh',1600,350,100,75,100,270,480,1300,25,35,3010,1,100),
(7,'Hư Không Thú',2100,400,120,85,100,270,480,1200,30,40,3010,1,100),
(8,'Cổ Ma',2800,480,150,85,100,280,500,1200,30,50,3010,1,100);

UPDATE map_template SET mob = JSON_ARRAY(
    JSON_OBJECT('id',1,'templateId',id DIV 2,'x',1000,'y',600),
    JSON_OBJECT('id',2,'templateId',id DIV 2,'x',800,'y',450),
    JSON_OBJECT('id',3,'templateId',id DIV 2,'x',1100,'y',500)
) WHERE type = 1 AND id BETWEEN 2 AND 16 AND JSON_LENGTH(mob) = 0;

INSERT IGNORE INTO tutien_schema(id,name) VALUES(4,'Milestone 1: monsters, durable drops and kill receipts');
